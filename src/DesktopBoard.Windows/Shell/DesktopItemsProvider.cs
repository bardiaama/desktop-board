using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;
using DesktopBoard.Core.Services;

namespace DesktopBoard.Windows.Shell;

/// <summary>
/// Enumerates the user's and the public Desktop folders plus the Recycle Bin, and renders
/// each item's icon through the shell (IShellItemImageFactory), so the board can show the
/// same icons Windows would. Watches both folders for changes.
/// </summary>
public sealed class DesktopItemsProvider : IDesktopItemsProvider
{
    private const string RecycleBin = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly System.Threading.Timer _debounce;

    public DesktopItemsProvider()
    {
        _debounce = new System.Threading.Timer(_ => Changed?.Invoke(this, EventArgs.Empty));
        foreach (var dir in DesktopFolders())
        {
            try
            {
                var w = new FileSystemWatcher(dir) { IncludeSubdirectories = false, EnableRaisingEvents = true };
                FileSystemEventHandler h = (_, _) => _debounce.Change(700, Timeout.Infinite);
                w.Created += h; w.Deleted += h; w.Changed += h;
                w.Renamed += (_, _) => _debounce.Change(700, Timeout.Infinite);
                _watchers.Add(w);
            }
            catch (Exception ex) { Logger.Warn($"Cannot watch {dir}: {ex.Message}"); }
        }
    }

    public event EventHandler? Changed;

    public static IEnumerable<string> DesktopFolders()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (Directory.Exists(user)) yield return user;
        if (Directory.Exists(common) && !string.Equals(common, user, StringComparison.OrdinalIgnoreCase)) yield return common;
    }

    public Task<IReadOnlyList<DesktopItem>> GetItemsAsync(int iconSizePx) => Task.Run(() =>
    {
        var list = new List<DesktopItem>();
        try
        {
            list.Add(Build("Recycle Bin", RecycleBin, isVirtual: true, isFolder: true, iconSizePx));
        }
        catch (Exception ex) { Logger.Warn("Recycle Bin item failed: " + ex.Message); }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in DesktopFolders())
        {
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (Exception ex) { Logger.Warn($"Cannot read {dir}: {ex.Message}"); continue; }

            foreach (var path in entries)
            {
                var name = System.IO.Path.GetFileName(path);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var attr = File.GetAttributes(path);
                    if ((attr & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    var isFolder = (attr & FileAttributes.Directory) != 0;
                    var display = name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".url", StringComparison.OrdinalIgnoreCase)
                        ? System.IO.Path.GetFileNameWithoutExtension(name) : name;
                    if (!seen.Add(display)) continue;
                    list.Add(Build(display, path, isVirtual: false, isFolder, iconSizePx));
                }
                catch (Exception ex) { Logger.Warn($"Desktop item skipped {path}: {ex.Message}"); }
            }
        }

        // Recycle Bin first, then folders, then everything else, alphabetically (culture-aware for Persian names).
        var ordered = list
            .OrderBy(i => i.IsVirtual ? 0 : i.IsFolder ? 1 : 2)
            .ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return (IReadOnlyList<DesktopItem>)ordered;
    });

    public void Launch(DesktopItem item)
    {
        var target = item.IsVirtual ? "shell:RecycleBinFolder" : item.Path;
        var psi = item.IsVirtual
            ? new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true }
            : new ProcessStartInfo(target) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(target) ?? string.Empty };
        Process.Start(psi);
    }

    public void ShowInFolder(DesktopItem item)
    {
        if (item.IsVirtual) { Launch(item); return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Path}\"") { UseShellExecute = true });
    }

    private static DesktopItem Build(string name, string path, bool isVirtual, bool isFolder, int size)
    {
        var (bgra, w, h) = ShellIcons.Render(path, size);
        return new DesktopItem { Name = name, Path = path, IsVirtual = isVirtual, IsFolder = isFolder, IconBgra = bgra, IconWidth = w, IconHeight = h };
    }

    public void Dispose()
    {
        foreach (var w in _watchers) w.Dispose();
        _watchers.Clear();
        _debounce.Dispose();
    }
}

/// <summary>Shell icon rasterization: IShellItemImageFactory → HBITMAP → premultiplied BGRA bytes.</summary>
internal static class ShellIcons
{
    private static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out nint phbm);
    }

    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth; public int biHeight; public ushort biPlanes; public ushort biBitCount;
        public uint biCompression; public uint biSizeImage; public int biXPelsPerMeter; public int biYPelsPerMeter; public uint biClrUsed; public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public nint bmBits; }

    private const int SIIGBF_RESIZETOFIT = 0x0;
    private const int SIIGBF_BIGGERSIZEOK = 0x1;
    private const int SIIGBF_ICONONLY = 0x4;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, nint pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
    [DllImport("gdi32.dll")] private static extern int GetObject(nint h, int c, out BITMAP pv);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint h);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(nint hdc, nint hbm, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint hdc);

    public static (byte[]? Bgra, int Width, int Height) Render(string path, int size)
    {
        nint hbm = nint.Zero;
        try
        {
            var iid = IID_IShellItemImageFactory;
            SHCreateItemFromParsingName(path, nint.Zero, ref iid, out var factory);
            var hr = factory.GetImage(new SIZE { cx = size, cy = size }, SIIGBF_ICONONLY | SIIGBF_BIGGERSIZEOK, out hbm);
            Marshal.ReleaseComObject(factory);
            if (hr != 0 || hbm == nint.Zero) return (null, 0, 0);

            GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm);
            var w = bm.bmWidth;
            var h = Math.Abs(bm.bmHeight);
            var info = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0
            };
            var bytes = new byte[w * h * 4];
            var hdc = GetDC(nint.Zero);
            try
            {
                if (GetDIBits(hdc, hbm, 0, (uint)h, bytes, ref info, 0) == 0) return (null, 0, 0);
            }
            finally { ReleaseDC(nint.Zero, hdc); }

            // Shell bitmaps carry straight alpha; WinUI wants premultiplied.
            var anyAlpha = false;
            for (var i = 3; i < bytes.Length; i += 4) if (bytes[i] != 0) { anyAlpha = true; break; }
            for (var i = 0; i < bytes.Length; i += 4)
            {
                var a = bytes[i + 3];
                if (!anyAlpha) { bytes[i + 3] = 255; continue; } // bitmap without an alpha channel: treat as opaque
                if (a == 255) continue;
                bytes[i] = (byte)(bytes[i] * a / 255);
                bytes[i + 1] = (byte)(bytes[i + 1] * a / 255);
                bytes[i + 2] = (byte)(bytes[i + 2] * a / 255);
            }
            return (bytes, w, h);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Icon render failed for {path}: {ex.Message}");
            return (null, 0, 0);
        }
        finally
        {
            if (hbm != nint.Zero) DeleteObject(hbm);
        }
    }
}
