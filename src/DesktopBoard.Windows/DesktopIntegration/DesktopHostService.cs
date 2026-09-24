using System.Runtime.InteropServices;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Services;
using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.DesktopIntegration;

/// <summary>
/// Puts the board window into the desktop layer. Every Win32 call is contained here so the
/// rest of the app only sees <see cref="IDesktopHostService"/>.
///
/// Modes:
///   * <b>BottomMost</b> (default): a top-level tool window pinned to the bottom of the
///     Z-order by intercepting WM_WINDOWPOSCHANGING, sized to the monitor's work area
///     (taskbar excluded) minus the strip occupied by desktop icons. It receives input
///     like any window, stays under every other application, and un-minimizes itself
///     after "Show desktop".
///   * <b>Embedded</b>: re-parented into the shell's WorkerW behind the icon layer (see
///     <see cref="WorkerWLocator"/>). Looks like part of the wallpaper but Windows routes
///     all desktop input to the icon list view above it, so it is display-only.
///   * <b>Normal</b>: a plain borderless window (used by --size= previews).
///
/// A WS_CHILD window never receives WM_DISPLAYCHANGE, and a bottom-most window may miss
/// it while another app is foreground, so <see cref="RefreshIfChanged"/> is also polled
/// from a slow UI timer; it only touches the window when geometry actually changed.
/// </summary>
public sealed class DesktopHostService : IDesktopHostService
{
    private const uint WM_DISPLAYCHANGE = 0x007E;
    private const uint WM_DPICHANGED = 0x02E0;
    private const uint WM_DPICHANGED_AFTERPARENT = 0x02E3;
    private const uint WM_APP_RECHECK_DESKTOP = 0x8000 + 41;
    private System.Threading.Timer? _recheck;

    private WndProc? _subclassProc;   // kept alive for the lifetime of the subclass
    private nint _originalWndProc;
    private nint _subclassedHwnd;
    private nint _hwnd;
    private bool _refreshing;
    private (int X, int Y, int Width, int Height) _applied;
    private nint _appliedParent;
    private (int X, int Y, int Width, int Height)? _fixedBounds;
    private bool _autoInset = true;
    private int _extraInsetLogical;
    private WinEventProc? _winEventProc;   // kept alive while the hook is installed
    private nint _winEventHook;
    private bool _desktopRaised;

    public DesktopHostMode CurrentMode { get; private set; } = DesktopHostMode.Normal;

    public void SetLeftInset(bool auto, int extraLogicalPx)
    {
        _autoInset = auto;
        _extraInsetLogical = Math.Max(0, extraLogicalPx);
    }

    public DesktopHostResult Attach(nint hwnd, DesktopHostMode requestedMode)
    {
        if (hwnd == nint.Zero || !IsWindow(hwnd))
            return new DesktopHostResult(DesktopHostMode.Normal, "Invalid window handle.");

        _hwnd = hwnd;
        var detail = string.Empty;

        if (requestedMode is DesktopHostMode.Embedded)
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
                InstallShowDesktopWatch();
                return new DesktopHostResult(CurrentMode, detail + $"Bottom-most desktop window over the work area {_applied.Width}x{_applied.Height} at ({_applied.X},{_applied.Y}).");
            }
            catch (Exception ex)
            {
                detail += "Bottom-most failed: " + ex.Message + ". ";
            }
        }

        // Normal mode: the window keeps whatever size the app gave it (e.g. --size= previews).
        CurrentMode = DesktopHostMode.Normal;
        if (GetWindowRect(hwnd, out var r))
            _fixedBounds = (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        InstallSubclass(hwnd);
        return new DesktopHostResult(CurrentMode, detail + "Running as a normal borderless window.");
    }

    public void Detach(nint hwnd)
    {
        RemoveShowDesktopWatch();
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
        var (mx, my, mw, mh, wx, wy, ww, wh) = PrimaryMonitor();
        // Embedded windows live under the taskbar anyway; interactive modes stay inside the work area.
        var (x, y, w, h) = CurrentMode == DesktopHostMode.Embedded ? (mx, my, mw, mh) : (wx, wy, ww, wh);

        var dpi = _hwnd != nint.Zero && IsWindow(_hwnd) ? GetDpiForWindow(_hwnd) : GetDpiForSystem();
        var inset = (int)Math.Round(_extraInsetLogical * Math.Max(96u, dpi) / 96.0);
        if (_autoInset && CurrentMode != DesktopHostMode.Embedded)
            inset += DesktopIconsService.GetOccupiedLeftWidth(wh);
        inset = Math.Clamp(inset, 0, w / 3);

        return (x + inset, y, w - inset, h);
    }

    public (int X, int Y, int Width, int Height) GetPrimaryMonitorBounds()
    {
        var (mx, my, mw, mh, _, _, _, _) = PrimaryMonitor();
        return (mx, my, mw, mh);
    }

    public bool RefreshIfChanged()
    {
        if (_hwnd == nint.Zero || !IsWindow(_hwnd) || CurrentMode == DesktopHostMode.Normal) return false;
        if (CurrentMode == DesktopHostMode.BottomMost) UpdateShowDesktopState();
        var target = GetTargetBounds();
        var parent = GetParent(_hwnd);
        var parentOk = CurrentMode != DesktopHostMode.Embedded || (parent != nint.Zero && IsWindow(parent) && parent == _appliedParent);
        if (target == _applied && parentOk) return false;
        Refresh();
        return true;
    }

    // ---------------------------------------------------------------------------------

    private static (int mx, int my, int mw, int mh, int wx, int wy, int ww, int wh) PrimaryMonitor()
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        var hmon = MonitorFromPoint(new POINT { X = 0, Y = 0 }, MONITOR_DEFAULTTOPRIMARY);
        if (hmon != nint.Zero && GetMonitorInfo(hmon, ref mi))
        {
            var m = mi.rcMonitor;
            var w = mi.rcWork;
            return (m.Left, m.Top, m.Right - m.Left, m.Bottom - m.Top, w.Left, w.Top, w.Right - w.Left, w.Bottom - w.Top);
        }
        var cx = GetSystemMetrics(0);
        var cy = GetSystemMetrics(1);
        return (0, 0, cx, cy, 0, 0, cx, cy);
    }

    private (bool Ok, string Detail) TryEmbed(nint hwnd)
    {
        var (mx, my, mw, mh, _, _, _, _) = PrimaryMonitor();
        var located = WorkerWLocator.Locate((mx, my, mw, mh));
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
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)((style & ~WS_CHILD) | WS_POPUP));
            return (false, $"SetParent failed (Win32 error {err})");
        }

        if (GetParent(hwnd) != located.WorkerW)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, (nint)((style & ~WS_CHILD) | WS_POPUP));
            SetParent(hwnd, nint.Zero);
            return (false, "SetParent did not take effect");
        }

        CurrentMode = DesktopHostMode.Embedded;
        FitEmbedded(hwnd);
        return (true, $"Embedded in desktop ({located.Layout} layout, WorkerW 0x{located.WorkerW.ToInt64():X}). Display-only: the icon layer takes all desktop input.");
    }

    /// <summary>
    /// Sizes the embedded child. Child coordinates are relative to the parent's client area;
    /// the classic (top-level) WorkerW starts at the virtual-screen origin, the 24H2
    /// per-monitor WorkerW at its own monitor's origin.
    /// </summary>
    private void FitEmbedded(nint hwnd)
    {
        var target = GetTargetBounds();
        var (x, y, w, h) = target;
        var parent = GetParent(hwnd);
        int ox, oy;
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
        _applied = target;
        _appliedParent = parent;
    }

    private void ApplyBottomMost(nint hwnd)
    {
        CurrentMode = DesktopHostMode.BottomMost;
        var target = GetTargetBounds();
        var (x, y, w, h) = target;

        var ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        ex &= ~WS_EX_APPWINDOW;
        ex |= WS_EX_TOOLWINDOW;
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)ex);

        SetWindowPos(hwnd, HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        _applied = target;
        _appliedParent = nint.Zero;
    }

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
                    var (mx, my, mw, mh, _, _, _, _) = PrimaryMonitor();
                    var located = WorkerWLocator.Locate((mx, my, mw, mh));
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
                    var target = GetTargetBounds();
                    var (x, y, w, h) = target;
                    SetWindowPos(_hwnd, HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    _applied = target;
                    break;
                }
                default:
                    if (_fixedBounds is { } fb)
                        SetWindowPos(_hwnd, nint.Zero, fb.X, fb.Y, fb.Width, fb.Height, SWP_NOACTIVATE | SWP_NOZORDER | SWP_FRAMECHANGED);
                    return;
            }
            Logger.Info($"Desktop host: refit to {_applied.Width}x{_applied.Height} at ({_applied.X},{_applied.Y}) ({CurrentMode}).");
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

    // ---- "Show desktop" (Win+D) --------------------------------------------------------
    // Windows 11 implements Show desktop by raising Progman to the top of the Z-order and
    // minimizing the app windows; a bottom-most window would end up hidden under the
    // desktop. A foreground-change WinEvent (no polling, no injection) tells us when that
    // happens: while the desktop is raised the board is placed directly above Progman,
    // and it drops back to the bottom as soon as any other window comes to the front.

    private void InstallShowDesktopWatch()
    {
        RemoveShowDesktopWatch();
        _winEventProc = OnWinEvent;
        _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZEEND, nint.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
        UpdateShowDesktopState();
    }

    private void RemoveShowDesktopWatch()
    {
        _recheck?.Dispose();
        _recheck = null;
        if (_winEventHook != nint.Zero) UnhookWinEvent(_winEventHook);
        _winEventHook = nint.Zero;
        _winEventProc = null;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (eventType is not (EVENT_SYSTEM_FOREGROUND or EVENT_SYSTEM_MINIMIZEEND)) return;
        UpdateShowDesktopState();
        // The shell re-orders windows shortly *after* the foreground change; look again twice.
        _recheck ??= new System.Threading.Timer(_ => { if (_hwnd != nint.Zero) PostMessage(_hwnd, WM_APP_RECHECK_DESKTOP, 0, 0); });
        _recheck.Change(350, 900);
    }

    /// <summary>
    /// True while Show desktop is active: the shell has raised Progman, so other (minimized)
    /// app windows now sit *below* it in the Z-order. Normally Progman is the very last window.
    /// The board itself is ignored because it deliberately moves above Progman in that state.
    /// </summary>
    private bool IsDesktopRaised()
    {
        var progman = FindWindow("Progman", null);
        if (progman == nint.Zero) return false;
        var w = GetWindow(progman, GW_HWNDNEXT);
        var guard = 0;
        while (w != nint.Zero && guard++ < 4096)
        {
            if (w != _hwnd && IsWindowVisible(w)) return true;
            w = GetWindow(w, GW_HWNDNEXT);
        }
        return false;
    }

    private void UpdateShowDesktopState()
    {
        if (_hwnd == nint.Zero || !IsWindow(_hwnd) || CurrentMode != DesktopHostMode.BottomMost) return;
        var raised = IsDesktopRaised();
        if (raised == _desktopRaised) return;
        _desktopRaised = raised;
        if (raised)
        {
            // Directly above the raised desktop: below whatever precedes Progman (topmost band), else top.
            var progman = FindWindow("Progman", null);
            var above = progman == nint.Zero ? nint.Zero : GetWindow(progman, GW_HWNDPREV);
            SetWindowPos(_hwnd, above == nint.Zero ? HWND_TOP : above, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        Logger.Info(raised ? "Desktop host: Show desktop active, board raised above Progman." : "Desktop host: board back at the bottom.");
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
            case WM_WINDOWPOSCHANGING when CurrentMode == DesktopHostMode.BottomMost && !_desktopRaised:
            {
                // Any Z-order change (activation, another app's SetWindowPos) is redirected to the bottom.
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
            case WM_SIZE when CurrentMode == DesktopHostMode.BottomMost && wParam.ToInt64() == SIZE_MINIMIZED:
            {
                // "Show desktop" (Win+D) minimizes every top-level window; the board is part of
                // the desktop, so it comes straight back without taking activation.
                var result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                ShowWindow(hWnd, SW_SHOWNOACTIVATE);
                var (x, y, w, h) = _applied;
                SetWindowPos(hWnd, HWND_BOTTOM, x, y, w, h, SWP_NOACTIVATE);
                return result;
            }
            case WM_APP_RECHECK_DESKTOP:
                UpdateShowDesktopState();
                _recheck?.Change(Timeout.Infinite, Timeout.Infinite);
                return nint.Zero;
            case WM_DISPLAYCHANGE:
            case WM_DPICHANGED:
            case WM_DPICHANGED_AFTERPARENT:
            {
                var result = CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
                if (CurrentMode != DesktopHostMode.Normal) Refresh();
                return result;
            }
        }
        return CallWindowProc(_originalWndProc, hWnd, msg, wParam, lParam);
    }
}
