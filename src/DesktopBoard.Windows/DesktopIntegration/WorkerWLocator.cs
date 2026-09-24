using static DesktopBoard.Windows.NativeInterop.NativeMethods;

namespace DesktopBoard.Windows.DesktopIntegration;

/// <summary>
/// Finds the shell window that sits between the wallpaper and the desktop icons.
///
/// Background: the desktop is drawn by explorer's "Progman" window. Its child
/// "SHELLDLL_DefView" hosts the icons. Sending Progman the undocumented message
/// 0x052C makes it create a "WorkerW" window *behind* the icon layer (this is what
/// Windows uses for wallpaper transitions and what every live-wallpaper app relies on).
/// A window re-parented into that WorkerW is drawn above the wallpaper and below
/// the icons, and is unaffected by "Show desktop".
///
/// Two layouts exist:
///   * Classic (Windows 7 - 11 23H2): WorkerW windows are top-level siblings of
///     Progman. The icon DefView lives in one WorkerW; the *next* WorkerW is the
///     wallpaper layer we want.
///   * Windows 11 24H2+: DefView stays inside Progman and the spawned WorkerW is
///     a child of Progman, placed behind DefView.
/// </summary>
public static class WorkerWLocator
{
    public sealed record Result(nint Progman, nint WorkerW, nint DefView, string Layout);

    public static Result? Locate()
    {
        var progman = FindWindow("Progman", null);
        if (progman == nint.Zero) return null;

        // Ask Progman to spawn the WorkerW. Both parameter forms are used in the wild;
        // the second is the one the shell itself uses on newer builds.
        SendMessageTimeout(progman, WM_SPAWN_WORKER, 0xD, 0x1, SMTO_NORMAL, 1000, out _);
        SendMessageTimeout(progman, WM_SPAWN_WORKER, 0, 0, SMTO_NORMAL, 1000, out _);

        // Layout 1: DefView inside a top-level WorkerW; wallpaper WorkerW is its next sibling.
        nint workerW = nint.Zero, defView = nint.Zero;
        EnumWindows((top, _) =>
        {
            var dv = FindWindowEx(top, nint.Zero, "SHELLDLL_DefView", null);
            if (dv != nint.Zero && GetClassName(top) == "WorkerW")
            {
                defView = dv;
                workerW = FindWindowEx(nint.Zero, top, "WorkerW", null);
                return false;
            }
            return true;
        }, nint.Zero);

        if (workerW != nint.Zero && IsWindow(workerW))
            return new Result(progman, workerW, defView, "classic");

        // Layout 2 (24H2+): DefView and WorkerW are both children of Progman.
        defView = FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
        if (defView != nint.Zero)
        {
            var child = FindWindowEx(progman, nint.Zero, "WorkerW", null);
            if (child != nint.Zero && IsWindow(child))
                return new Result(progman, child, defView, "progman-child");

            // No WorkerW spawned: use Progman itself and rely on Z-ordering below DefView.
            return new Result(progman, progman, defView, "progman-direct");
        }

        return null;
    }
}
