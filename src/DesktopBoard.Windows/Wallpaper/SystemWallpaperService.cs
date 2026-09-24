using System.Text;
using DesktopBoard.Core.Interfaces;
using Microsoft.Win32;
using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.Wallpaper;

/// <summary>
/// Returns the wallpaper Windows is currently showing, so the board can render the same
/// image behind its glass panels and look like a native part of the desktop.
/// </summary>
public sealed class SystemWallpaperService : ISystemWallpaperService
{
    public string? GetCurrentWallpaperPath()
    {
        var sb = new StringBuilder(1024);
        if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
        {
            var p = sb.ToString();
            if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) return p;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (key?.GetValue("WallPaper") is string reg && File.Exists(reg)) return reg;
        }
        catch { /* registry unavailable: fall through */ }

        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        return File.Exists(transcoded) ? transcoded : null;
    }
}
