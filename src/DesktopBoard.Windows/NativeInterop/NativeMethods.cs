using System.Runtime.InteropServices;
using System.Text;

namespace DesktopBoard.Windows.NativeInterop;

/// <summary>Minimal user32 surface used by desktop integration. Kept in one place on purpose.</summary>
internal static partial class NativeMethods
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int GWLP_WNDPROC = -4;

    public const long WS_CHILD = 0x40000000L;
    public const long WS_POPUP = 0x80000000L;
    public const long WS_CAPTION = 0x00C00000L;
    public const long WS_THICKFRAME = 0x00040000L;
    public const long WS_MINIMIZEBOX = 0x00020000L;
    public const long WS_MAXIMIZEBOX = 0x00010000L;
    public const long WS_SYSMENU = 0x00080000L;
    public const long WS_VISIBLE = 0x10000000L;

    public const long WS_EX_APPWINDOW = 0x00040000L;
    public const long WS_EX_TOOLWINDOW = 0x00000080L;
    public const long WS_EX_NOACTIVATE = 0x08000000L;

    public static readonly nint HWND_BOTTOM = new(1);
    public static readonly nint HWND_TOP = nint.Zero;
    public static readonly nint HWND_NOTOPMOST = new(-2);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const uint WM_SIZE = 0x0005;
    public const int SIZE_MINIMIZED = 1;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint LVM_GETITEMCOUNT = 0x1004;
    public const uint LVM_GETITEMSPACING = 0x1033;
    public const uint WM_WINDOWPOSCHANGING = 0x0046;
    public const uint WM_SYSCOMMAND = 0x0112;
    public const uint WM_SPAWN_WORKER = 0x052C;
    public const int SC_MINIMIZE = 0xF020;

    public const uint SMTO_NORMAL = 0x0000;

    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    public const uint MONITOR_DEFAULTTOPRIMARY = 1;
    public const uint SPI_GETDESKWALLPAPER = 0x0073;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public nint hwnd;
        public nint hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint GW_HWNDNEXT = 2;
    public const uint GW_HWNDPREV = 3;

    public delegate void WinEventProc(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime);
    [DllImport("user32.dll")] public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmodWinEventProc, WinEventProc pfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] public static extern bool UnhookWinEvent(nint hWinEventHook);
    [DllImport("user32.dll")] public static extern nint GetTopWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern nint GetWindow(nint hWnd, uint uCmd);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    public delegate nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)] public static extern nint FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint FindWindowEx(nint hwndParent, nint hwndChildAfter, string? lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SetParent(nint hWndChild, nint hWndNewParent);
    [DllImport("user32.dll")] public static extern nint GetParent(nint hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll", SetLastError = true)] public static extern nint SendMessageTimeout(nint hWnd, uint Msg, nint wParam, nint lParam, uint fuFlags, uint uTimeout, out nint lpdwResult);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(nint hWnd);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] public static extern nint MonitorFromPoint(POINT pt, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll")] public static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern nint DefWindowProc(nint hWnd, uint Msg, nint wParam, nint lParam);
    [DllImport("user32.dll")] public static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);
    [DllImport("user32.dll")] public static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong32(nint hWnd, int nIndex);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    public static nint GetWindowLongPtr(nint hWnd, int nIndex) =>
        nint.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);

    public static nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong) =>
        nint.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLong32(hWnd, nIndex, (int)dwNewLong);

    public static string GetClassName(nint hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
