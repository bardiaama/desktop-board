using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Services;
using Microsoft.Win32;
using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.Shell;

/// <summary>
/// The shell's "Show desktop icons" toggle. Explorer stores the state in
/// HKCU\...\Explorer\Advanced\HideIcons and flips it when its desktop view receives the
/// same WM_COMMAND (0x7402) as the context-menu item, which repaints immediately without
/// restarting explorer.
/// </summary>
public sealed class ShellIconsService : IShellIconsService
{
    private const uint WM_COMMAND = 0x0111;
    private const int ToggleDesktopIconsCommand = 0x7402;
    private const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    public bool AreVisible()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AdvancedKey);
            return key?.GetValue("HideIcons") is not int hide || hide == 0;
        }
        catch { return true; }
    }

    public void SetVisible(bool visible)
    {
        if (AreVisible() == visible) return;
        var defView = FindDefView();
        if (defView == nint.Zero)
        {
            Logger.Warn("Shell icons: SHELLDLL_DefView not found; cannot toggle.");
            return;
        }
        SendMessageTimeout(defView, WM_COMMAND, ToggleDesktopIconsCommand, 0, SMTO_NORMAL, 2000, out _);
        Logger.Info(visible ? "Shell desktop icons shown." : "Shell desktop icons hidden (board dock active).");
    }

    private static nint FindDefView()
    {
        var progman = FindWindow("Progman", null);
        var defView = progman == nint.Zero ? nint.Zero : FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
        if (defView != nint.Zero) return defView;
        EnumWindows((top, _) =>
        {
            var dv = FindWindowEx(top, nint.Zero, "SHELLDLL_DefView", null);
            if (dv != nint.Zero) { defView = dv; return false; }
            return true;
        }, nint.Zero);
        return defView;
    }
}
