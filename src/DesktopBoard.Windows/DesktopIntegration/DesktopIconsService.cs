using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.DesktopIntegration;

/// <summary>
/// Estimates how much of the left edge of the desktop is occupied by icons, so the board can
/// leave that strip uncovered. Uses only messages that need no memory in explorer's process:
/// the icon count and the grid spacing. Assumes the usual auto-arranged column layout
/// (icons fill columns from the left); manually scattered icons are not detected.
/// </summary>
public static class DesktopIconsService
{
    /// <param name="desktopHeightPx">Height available for icon columns (physical px).</param>
    /// <returns>Width in physical pixels occupied by icon columns, or 0 when unknown.</returns>
    public static int GetOccupiedLeftWidth(int desktopHeightPx)
    {
        try
        {
            var listView = FindDesktopListView();
            if (listView == nint.Zero) return 0;

            var count = (int)SendMessage(listView, LVM_GETITEMCOUNT, 0, 0);
            if (count <= 0) return 0;

            var spacing = SendMessage(listView, LVM_GETITEMSPACING, 0, 0).ToInt64();
            var dx = (int)(spacing & 0xFFFF);
            var dy = (int)((spacing >> 16) & 0xFFFF);
            if (dx <= 0 || dy <= 0 || desktopHeightPx <= 0) return 0;

            var rows = Math.Max(1, desktopHeightPx / dy);
            var columns = (count + rows - 1) / rows;
            return columns * dx;
        }
        catch
        {
            return 0;
        }
    }

    private static nint FindDesktopListView()
    {
        var progman = FindWindow("Progman", null);
        var defView = progman == nint.Zero ? nint.Zero : FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
        if (defView == nint.Zero)
        {
            // Classic layout: DefView lives inside a top-level WorkerW.
            EnumWindows((top, _) =>
            {
                var dv = FindWindowEx(top, nint.Zero, "SHELLDLL_DefView", null);
                if (dv != nint.Zero) { defView = dv; return false; }
                return true;
            }, nint.Zero);
        }
        return defView == nint.Zero ? nint.Zero : FindWindowEx(defView, nint.Zero, "SysListView32", null);
    }
}
