using System.Runtime.InteropServices;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Services;
using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.DesktopIntegration;

/// <summary>
/// Puts the board window into the desktop layer. See <see cref="WorkerWLocator"/> for the
/// shell mechanics. Every Win32 call is contained here so the rest of the app only sees
/// <see cref="IDesktopHostService"/>.
///
/// After attaching, the window is subclassed so that:
///   * display changes (dock/undock, resolution or DPI change) re-fit the board to the
///     primary monitor and re-attach it if the shell recreated its WorkerW;
///   * in BottomMost mode the window is pinned to the bottom of the Z-order;
///   * minimize requests (e.g. "Show desktop" in fallback mode) are ignored.
/// </summary>
public sealed class DesktopHostService : IDesktopHostService
{
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_DPICHANGED = 0x02E0;
    private const uint WM_SETTINGCHANGE = 0x001A;

    private WndProc? _subclassProc;   // kept alive for the lifetime of the subclass
    private nint _originalWndProc;
    private nint _subclassedHwnd;
    private nint _hwnd;
    private bool _refreshing;

    public DesktopHostMode CurrentMode { get; private set; } = DesktopHostMode.Normal;

    public DesktopHostResult Attach(nint hwnd, DesktopHostMode requestedMode)
    {
        if (hwnd == nint.Zero || !IsWindow(hwnd))
            return new DesktopHostResult(DesktopHostMode.Normal, "Invalid window handle.");

        _hwnd = hwnd;
        var detail = string.Empty;

        if (requestedMode is DesktopHostMode.Auto or DesktopHostMode.Embedded)
        {
            try
            {
                var (ok, why) = TryEmbed(hwnd);
                if (ok)
                {
                    CurrentMode = DesktopHostMode.Embedded;
                    InstallSubclass(hwnd);
                    return new DesktopHostResult(CurrentMode, why);
                }
                detail = "Embedding failed: " + why + ". ";
            }
            catch (Exception ex)
            {
                detail = "Embedding threw: " + ex.Message + ". ";
            }
        }

        if (requestedMode is DesktopHostMode.Auto or DesktopHostMode.Embedded or DesktopHostMode.BottomMost)
        {
            try
            {
                ApplyBottomMost(hwnd);
                CurrentMode = DesktopHostMode.BottomMost;
                InstallSubclass(hwnd);
                return new DesktopHostResult(CurrentMode, detail + "Running as a bottom-most desktop window.");
            }
            catch (Exception ex)
            {
                detail += "Bottom-most failed: " + ex.Message + ". ";
            }
        }

        CurrentMode = DesktopHostMode.Normal;
        InstallSubclass(hwnd);
        return new DesktopHostResult(CurrentMode, detail + "Running as a normal borderless window.");
    }

    public void Detach(nint hwnd)
    {
        RemoveSubclass();
        if (hwnd == nint.Zero || !IsWindow(hwnd)) return;
        if (GetParent(hwnd) != nint.Zero)
        {
            var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            style &= ~WS_CHILD;
            style |= WS_POPUP;
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)style);
            SetParent(hwnd, nint.Zero);
        }
        CurrentMode = DesktopHostMode.Normal;
    }

    public (int X, int Y, int Width, int Height) GetTargetBounds()
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        var hmon = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        if (hmon != nint.Zero && GetMonitorInfo(hmon, ref mi))
        {
            var r = mi.rcMonitor;
            return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        }
        return (0, 0, GetSystemMetrics(0), GetSystemMetrics(1));
    }

    // ---------------------------------------------------------------------------------

    private (bool Ok, string Detail) TryEmbed(nint hwnd)
    {
        var located = WorkerWLocator.Locate();
        if (located is null) return (false, "Progman / WorkerW not found");

        var style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
        style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        style |= WS_CHILD | WS_VISIBLE;
        SetWindowLongPtr(hwnd, GWL_STYLE, (nint)style);

        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex &= ~WS_EX_APPWINDOW;
        ex |= WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);

        var previous = SetParent(hwnd, located.WorkerW);
        if (previous == nint.Zero && Marshal.GetLastWin32Error() != 0)
        {
            var err = Marshal.GetLastWin32Error();
            // Undo the style change so the fallback modes get a sane window.
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)((style & ~WS_CHILD) | WS_POPUP));
            return (false, $"SetParent failed (Win32 error {err})");
        }

        if (GetParent(hwnd) != located.WorkerW)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)((style & ~WS_CHILD) | WS_POPUP));
            SetParent(hwnd, nint.Zero);
            return (false, "SetParent did not take effect");
        }

        FitEmbedded(hwnd);
        return (true, $"Embedded in desktop ({located.Layout} layout, WorkerW 0x{located.WorkerW.ToInt64():X}).");
    }

    /// <summary>
    /// Sizes the embedded child to the primary monitor. Child coordinates are relative to
    /// the parent's client area; the classic (top-level) WorkerW starts at the virtual-screen
    /// origin, while the 24H2 per-monitor WorkerW starts at its own monitor's origin.
    /// Bottom of the parent's child Z-order keeps us behind the icon view when the parent is
    /// Progman itself (24H2 "progman-direct" layout).
    /// </summary>
    private void FitEmbedded(nint hwnd)
    {
        var (x, y, w, h) = GetTargetBounds();
        var parent = GetParent(hwnd);
        int ox = 0, oy = 0;
        if (parent != nint.Zero && GetWindowRect(parent, out var pr))
        {
            ox = pr.Left;
            oy = pr.Top;
        }
        else
        {
            ox = GetSystemMetrics(SM_XVIRTUALSCREEN);
            oy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        }
        SetWindowPos(hwnd, HWND_BOTTOM, x - ox, y - oy, w, h, SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    private void ApplyBottomMost(nint hwnd)
    {
        var (x, y, w, h) = GetTargetBounds();

        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex &= ~WS_EX_APPWINDOW;
        ex |= WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);

        SetWindowPos(hwnd, HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
    }

    /// <summary>
    /// Re-fits the board after the display configuration changed. If the shell replaced its
    /// WorkerW (explorer does this on some display changes) the window is re-parented.
    /// </summary>
    private void Refresh()
    {
        if (_refreshing || _hwnd == nint.Zero || !IsWindow(_hwnd)) return;
        _refreshing = true;
        try
        {
            switch (CurrentMode)
            {
                case DesktopHostMode.Embedded:
                {
                    var parent = GetParent(_hwnd);
                    var located = WorkerWLocator.Locate();
                    if (located is not null && located.WorkerW != parent && IsWindow(located.WorkerW))
                    {
                        SetParent(_hwnd, located.WorkerW);
                        Logger.Info($"Desktop host: re-parented to WorkerW 0x{located.WorkerW.ToInt64():X} after display change ({located.Layout}).");
                    }
                    FitEmbedded(_hwnd);
                    break;
                }
                case DesktopHostMode.BottomMost:
                {
                    var (x, y, w, h) = GetTargetBounds();
                    SetWindowPos(_hwnd, HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    break;
                }
                default:
                {
                    var (x, y, w, h) = GetTargetBounds();
                    SetWindowPos(_hwnd, nint.Zero, x, y, w, h, SWP_NOACTIVATE | SWP_NOZORDER | SWP_FRAMECHANGED);
                    break;
                }
            }
            var (_, _, fw, fh) = GetTargetBounds();
            Logger.Info($"Desktop host: refit to {fw}x{fh} ({CurrentMode}).");
        }
        catch (Exception ex)
        {
            Logger.Error("Desktop host refresh failed", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void InstallSubclass(nint hwnd)
    {
        RemoveSubclass();
        _subclassProc = HostWndProc;
        _subclassedHwnd = hwnd;
        _originalWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_subclassProc));
    }

    private void RemoveSubclass()
    {
        if (_subclassedHwnd != nint.Zero && _originalWndProc != nint.Zero && IsWindow(_subclassedHwnd))
            SetWindowLongPtr(_subclassedHwnd, GWLP_WNDPROC, _originalWndProc);
        _subclassedHwnd = nint.Zero;
        _originalWndProc = nint.Zero;
        _subclassProc = null;
    }

    private nint HostWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_WINDOWPOSCHANGING when CurrentMode == DesktopHostMode.BottomMost:
            {
                var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if ((pos.flags & SWP_NOZORDER) == 0)
                {
                    pos.hwndInsertAfter = HWND_BOTTOM;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                break;
            }
            case WM_SYSCOMMAND when CurrentMode != DesktopHostMode.Normal && (wParam.ToInt64() & 0xFFF0) == SC_MINIMIZE:
                return nint.Zero;
            case WM_DISPLAYCHANGE:
            case WM_DPICHANGED:
            {
                var result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                Refresh();
                return result;
            }
        }
        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}
