using System.Runtime.InteropServices;

namespace DesktopBoard.Windows.NativeInterop;

/// <summary>
/// Classic Win32 open/save dialogs. Used instead of the WinRT pickers because those
/// need a top-level owner window, and the board window is a child of the desktop.
/// </summary>
public static class FileDialogs
{
    private const int MaxPath = 4096;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_PATHMUSTEXIST = 0x00000800;
    private const uint OFN_OVERWRITEPROMPT = 0x00000002;
    private const uint OFN_NOCHANGEDIR = 0x00000008;
    private const uint OFN_EXPLORER = 0x00080000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public nint hwndOwner;
        public nint hInstance;
        public string? lpstrFilter;
        public nint lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public nint lpstrFile;
        public int nMaxFile;
        public nint lpstrFileTitle;
        public int nMaxFileTitle;
        public string? lpstrInitialDir;
        public string? lpstrTitle;
        public uint Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string? lpstrDefExt;
        public nint lCustData;
        public nint lpfnHook;
        public nint lpTemplateName;
        public nint pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName(ref OPENFILENAME ofn);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileName(ref OPENFILENAME ofn);

    /// <param name="filter">Pairs of "Description|*.ext;*.ext2" joined by '|', e.g. "Images|*.jpg;*.png|All files|*.*".</param>
    public static string? Open(nint owner, string title, string filter, string? initialDir = null)
        => Show(owner, title, filter, initialDir, null, null, save: false);

    public static string? Save(nint owner, string title, string filter, string defaultExt, string? suggestedName = null)
        => Show(owner, title, filter, null, defaultExt, suggestedName, save: true);

    private static string? Show(nint owner, string title, string filter, string? initialDir, string? defaultExt, string? suggestedName, bool save)
    {
        var buffer = Marshal.AllocHGlobal(MaxPath * 2);
        try
        {
            // Zero the buffer, then copy the suggested name (if any).
            for (var i = 0; i < MaxPath * 2; i++) Marshal.WriteByte(buffer, i, 0);
            if (!string.IsNullOrEmpty(suggestedName))
            {
                var bytes = System.Text.Encoding.Unicode.GetBytes(suggestedName);
                Marshal.Copy(bytes, 0, buffer, Math.Min(bytes.Length, MaxPath * 2 - 2));
            }

            var ofn = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = owner,
                lpstrFilter = filter.Replace('|', '\0') + "\0\0",
                lpstrFile = buffer,
                nMaxFile = MaxPath,
                lpstrInitialDir = initialDir,
                lpstrTitle = title,
                lpstrDefExt = defaultExt,
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST)
            };

            var ok = save ? GetSaveFileName(ref ofn) : GetOpenFileName(ref ofn);
            if (!ok) return null;
            var result = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
