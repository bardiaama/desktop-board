# Desktop Board — Development Notes

Desktop Board turns the Windows desktop into a persistent, glassmorphic productivity
whiteboard (Work / کاری on the left, Personal / شخصی on the right). It is a WinUI 3
(Windows App SDK 1.8) unpackaged desktop app on .NET 8, with SQLite persistence.

## Solution layout

```text
DesktopBoard.sln
Directory.Build.props              nullable + implicit usings for every project
src/
  DesktopBoard.Core/               net8.0      models, interfaces, pure services (no UI, no Win32)
    Models/                        entities, enums, SettingKeys, SeedData, BoardSnapshot
    Interfaces/                    IRepository<T> + per-entity repos, ISettingsService,
                                   IBoardStateService, IBackupService, IDesktopHostService,
                                   IStartupService, ISystemWallpaperService
    Services/                      BoardStateService, SettingsService, Debouncer,
                                   SortOrderHelper, Logger
  DesktopBoard.Data/               net8.0      Microsoft.Data.Sqlite
    SQLite/SqliteDatabase.cs       single serialized connection (WAL), value helpers
    Migrations/Migrations.cs       IMigration + MigrationRunner + Migration001Initial
    Repositories/                  RepositoryBase<T> + Task/Goal/Project/Meeting/Note/
                                   StickyNote/Settings repositories
    BackupService.cs               JSON package export / import (BoardSnapshot)
    DatabaseInitializer.cs         migrations + first-run seed
  DesktopBoard.Windows/            net8.0-windows   Win32 only
    DesktopIntegration/            WorkerWLocator, DesktopHostService
    NativeInterop/                 NativeMethods (user32), FileDialogs (comdlg32)
    Startup/StartupService.cs      HKCU Run key
    Wallpaper/                     SystemWallpaperService (SPI_GETDESKWALLPAPER)
  DesktopBoard.App/                net8.0-windows10.0.19041.0   WinUI 3, MVVM
    Views/                         BoardView (the whole board), SettingsDialog
    ViewModels/                    MainViewModel + one VM per card / row
    Controls/                      BoardCard (templated), ChecklistControl,
                                   StickyBoardControl, LockToggle
    Converters/Fx.cs               static x:Bind functions
    Styles/                        Colors, Typography, Controls, Cards
    Services/                      UiStrings (fa / en / bilingual), WallpaperImageService
tests/
  DesktopBoard.Tests/              xunit: migrations, repositories, backup, Debouncer,
                                   BoardStateService, SortOrderHelper (12 tests)
```

Dependency direction: `App → Windows → Core`, `App → Data → Core`. Core has no
package references. UI never touches SQLite directly; Win32 never leaks past
`DesktopBoard.Windows`.

## Build

Requirements: Windows 10 1809+ (Windows 11 recommended), .NET SDK 8.0.4xx, internet for
the first NuGet restore. Visual Studio is **not** required — the Windows App SDK NuGet
package brings its own XAML compiler and the Windows SDK projection comes from NuGet.

```powershell
dotnet test tests/DesktopBoard.Tests                         # unit tests
dotnet build src/DesktopBoard.App -c Debug   -p:Platform=x64
dotnet build src/DesktopBoard.App -c Release -p:Platform=x64
# output: src/DesktopBoard.App/bin/x64/Release/net8.0-windows10.0.19041.0/win-x64/DesktopBoard.exe
```

The app is unpackaged (`WindowsPackageType=None`) and ships the Windows App SDK runtime
next to the exe (`WindowsAppSDKSelfContained=true`), so the only machine prerequisite is
the .NET 8 Desktop Runtime. It runs as the invoking user; no administrator rights.

Debug switches (command line):

- `--host=normal|bottommost|embedded|auto` overrides the desktop mode for one run.
- `--size=1920x1080` previews the layout at another resolution in a normal window.

Data lives in `%LOCALAPPDATA%\DesktopBoard\` (`board.db`, `desktopboard.log`).

## Architecture notes

- **MVVM**: CommunityToolkit.Mvvm `ObservableObject` / `[ObservableProperty]`; views use
  compiled `x:Bind` with static helper functions in `Converters/Fx.cs`. DI is
  `Microsoft.Extensions.DependencyInjection`, composed in `App.xaml.cs`.
- **Autosave**: every edit persists automatically. Text edits go through `Debouncer`
  (600–700 ms after the last keystroke); checkbox, color, reorder and drag-end writes
  are immediate. `MainViewModel.FlushAsync()` writes pending debounced edits on exit
  (the window cancels its first close, flushes, then closes).
- **Lock / edit**: `IBoardStateService` is the single source of truth. In LOCKED mode the
  whole body grid has `IsHitTestVisible=false` (no pointer input) and cancels
  `GettingFocus` (no keyboard focus), so neither a click nor Tab/Enter can reach any
  control; the header's lock pill is the only interactive element. Unlocking is a
  press-and-hold (650 ms, configurable off; when hold is on, Enter/Space on the pill do
  not unlock and only a primary-button press counts); locking is a click. `Ctrl+Shift+L`
  toggles, `Ctrl+Shift+K` locks. Default state on launch is LOCKED (setting
  `board.defaultLocked`), applied once at startup and never on a reload. Settings actions
  that rewrite data (reset layout, import) are disabled while locked, and import asks for
  confirmation.
- **Data model**: `Tasks` (Section × Category: Today / ThisWeek / Future), `Goals`,
  `Projects`, `Meetings`, `Notes` (one per section), `StickyNotes` (position, size,
  rotation, color), `AppSettings` (key/value), `SchemaVersion`. Dates are ISO strings,
  times are `HH:mm`, everything is UTF-8. New schema steps are added as `IMigration`
  classes with increasing versions; `MigrationRunner` applies them in a transaction.
- **Backup**: JSON package of a `BoardSnapshot`. The same DTO is the intended payload for
  a future sync service; import replaces all board data (settings are merged).
- **Wallpaper**: by default the board draws the user's current Windows wallpaper, so
  the embedded window looks like part of the desktop. The image is decoded once at
  ≤1280 px and box-blurred on the CPU (three passes) according to the blur setting; the
  darkness overlay is a simple rectangle. Cards use in-app `AcrylicBrush` (can be turned
  off in Settings for low-end machines).
- **UI scale**: the board is laid out at a virtual 1080 px height and rendered through a
  `ScaleTransform`. With no explicit setting the scale follows the screen height
  (1440p → 1.33, 1080p → 1.0, minimum 0.85); the Settings slider overrides it.
- **Persian**: all labels live in `UiStrings` (bilingual / fa / en). Row layout is
  left-to-right like the design (checkbox left, meta right) while every text element
  runs `FlowDirection=RightToLeft` for correct bidi of mixed Persian + English titles.
  `TextAlignment` in WinUI is logical (Left = start), which `Fx.Align` accounts for.
  The header shows the Jalali date (`PersianCalendar`) with Persian digits.

## Windows desktop integration (the important decisions)

Goal: the board sits directly above the wallpaper, every application window is above it,
it never covers the taskbar or the desktop icons, it survives "Show desktop", and it is
fully interactive.

**Why the board is not behind the desktop icons.** The classic wallpaper trick
(re-parenting into the shell's `WorkerW` behind `SHELLDLL_DefView`) was implemented and
verified visually, but Windows routes *all* desktop mouse and keyboard input to the icon
list view that sits above that layer; windows behind it never receive a click. Live
wallpaper apps work around this with global hooks that forward synthesized messages, and
WinUI 3 ignores synthesized legacy mouse messages (it only consumes real pointer input),
so that path is a dead end for an editable board. The mode is still available as
`Embedded` (display-only) in Settings.

`DesktopHostService.Attach` modes:

1. **BottomMost** (default, `auto`) — a top-level tool window pinned to the bottom of the
   Z-order by redirecting every `WM_WINDOWPOSCHANGING` to `HWND_BOTTOM`. It is placed
   over the primary monitor's **work area** (taskbar excluded) minus a left strip for the
   desktop icons. That strip is measured from the desktop list view (`LVM_GETITEMCOUNT` +
   `LVM_GETITEMSPACING`, no cross-process memory), assuming auto-arranged columns; the
   Settings slider adds extra pixels for manually placed icons.
   *Show desktop* (Win+D) on Windows 11 does not minimize the board (tool window); it
   raises `Progman` above everything and minimizes the apps, which would hide a
   bottom-most window. A `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (out-of-context, no
   injection) notices the change; while `Progman` has visible windows below it the board
   is placed directly above `Progman`, and it drops back to `HWND_BOTTOM` when any app
   comes to the front. Verified with real mouse input on this machine (build 26200).
2. **Embedded** — `WorkerWLocator` sends Progman `0x052C` and re-parents the HWND into
   the `WorkerW` behind the icon view. Both shell layouts are handled: classic top-level
   WorkerW siblings, and Windows 11 24H2+ where `SHELLDLL_DefView` and one `WorkerW` per
   monitor are children of Progman (the one whose rectangle contains the primary
   monitor is chosen). Display-only, see above.
3. **Normal** — a plain borderless window (also what `--size=` uses).

The mode is a setting (`desktop.hostMode`, default `auto`); changing it needs a restart.
The wallpaper copy behind the cards is laid out over the whole monitor and shifted by
the window offset, so it continues the real desktop around the board.

After attaching, the window is subclassed (`GWLP_WNDPROC`) for Z-order pinning and to
swallow minimize. Display changes (dock/undock, resolution, DPI) are detected by
`IDesktopHostService.RefreshIfChanged()`, which `MainWindow` calls from a 3-second UI
timer: a `WS_CHILD` window never receives `WM_DISPLAYCHANGE`, so the service compares the
primary monitor rectangle and the host WorkerW with what it last applied and only then
re-fits (and re-parents if explorer recreated its WorkerW). In the top-level fallback
modes the same refit also runs on `WM_DISPLAYCHANGE` / `WM_DPICHANGED`. `AppWindow`
becomes unusable after re-parenting, so the window is configured (borderless presenter,
size, switcher visibility) *before* `Attach`. On Windows 11 24H2 the per-monitor
WorkerW whose rectangle contains the primary monitor is chosen.

WinUI-specific gotchas met on the way (all fixed in code, kept here to save the next
person a day):

- A `ContentPresenter` whose `Content` is template-bound to a `null` property crashes
  layout with `E_INVALIDARG` ("Value does not fall within the expected range") and
  takes the process down. Use a `ContentControl` for optional slots.
- A `TransitionCollection` in a `Style` setter is shared between controls and throws the
  same error. Set transitions per element or not at all.
- `DesktopBoard.Windows` shadows the `Windows.*` namespaces inside `DesktopBoard.App`;
  use `global::Windows.…`.
- `ReleasePointerCapture` synchronously raises `PointerCaptureLost`; read state before
  releasing.

## Startup with Windows

`StartupService` writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DesktopBoard`
= `"<exe>" --autostart`. Per user, no elevation, visible in Task Manager → Startup. The
first run asks for consent (onboarding dialog); the Settings toggle changes it later.

## Known limitations

- Primary monitor only. Secondary monitors keep the plain wallpaper.
- Display changes are handled by re-fitting; if explorer *destroys* the WorkerW that hosts
  the board (rare, e.g. explorer restart), the board window is destroyed with it and the
  app must be started again. A watchdog / explorer-restart detection is a follow-up.
- In Embedded mode the board is a child of an explorer-owned window; WinRT pickers cannot
  own dialogs from it, so the classic Win32 open/save dialogs are used.
- Text in LOCKED mode is not selectable (by design: nothing on the body is hit-testable).
- The Segoe UI Variable / Segoe UI fonts render Persian well but a dedicated Persian
  face (e.g. Vazirmatn) would look better; bundling one is a follow-up.
- Reordering by drag works for tasks, goals and projects; cards themselves are fixed.

## Roadmap hooks

`BoardSnapshot` for sync, `IMigration` for schema evolution, `IDesktopHostService` for
replacing the shell integration, `UiStrings` for languages, `TaskCategory` and
`Meeting.Date` for calendar features, `SettingKeys` for new settings.
