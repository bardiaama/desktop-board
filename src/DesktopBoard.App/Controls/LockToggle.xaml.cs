using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopBoard.App.Controls;

/// <summary>
/// The "Board Locked / Edit Board" pill. Locking is one click. Unlocking is either a click
/// or, when <see cref="HoldToUnlock"/> is on, a press-and-hold with a progress bar so an
/// accidental click on the desktop can never open the board for editing.
/// </summary>
public sealed partial class LockToggle : UserControl
{
    private static readonly TimeSpan HoldDuration = TimeSpan.FromMilliseconds(650);

    private readonly DispatcherQueueTimer _holdTimer;
    private DateTime _holdStart;
    private bool _holding;
    private bool _pressed;

    public LockToggle()
    {
        InitializeComponent();
        _holdTimer = DispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(16);
        _holdTimer.IsRepeating = true;
        _holdTimer.Tick += HoldTimer_Tick;
        Loaded += (_, _) => UpdateState(false);
        KeyDown += LockToggle_KeyDown;
    }

    public event EventHandler? LockRequested;
    public event EventHandler? UnlockRequested;

    public static readonly DependencyProperty IsLockedProperty =
        DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(LockToggle), new PropertyMetadata(true, (d, _) => ((LockToggle)d).UpdateState(true)));

    public static readonly DependencyProperty HoldToUnlockProperty =
        DependencyProperty.Register(nameof(HoldToUnlock), typeof(bool), typeof(LockToggle), new PropertyMetadata(true));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(LockToggle), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(LockToggle), new PropertyMetadata(string.Empty));

    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    public bool HoldToUnlock { get => (bool)GetValue(HoldToUnlockProperty); set => SetValue(HoldToUnlockProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    private void UpdateState(bool transitions) =>
        VisualStateManager.GoToState(this, IsLocked ? "Locked" : "Unlocked", transitions);

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Only a primary (left / touch / pen tip) press counts; a right-click reaching for the
        // desktop context menu must never toggle the lock.
        var props = e.GetCurrentPoint(Root).Properties;
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch && !props.IsLeftButtonPressed)
            return;

        Focus(FocusState.Pointer);
        Root.CapturePointer(e.Pointer);
        _pressed = true;
        if (IsLocked && HoldToUnlock)
        {
            _holding = true;
            _holdStart = DateTime.UtcNow;
            _holdTimer.Start();
        }
        e.Handled = true;
    }

    private void Root_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        // Snapshot first: ReleasePointerCapture raises PointerCaptureLost synchronously,
        // which resets these flags.
        var wasPressed = _pressed;
        var wasHolding = _holding;
        _pressed = false;
        if (wasHolding)
        {
            // released before the hold completed: cancel
            CancelHold();
        }
        else if (wasPressed)
        {
            Toggle();
        }
        Root.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void Root_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_holding) CancelHold();
        _pressed = false;
    }

    private void HoldTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (!_holding) { _holdTimer.Stop(); return; }
        var t = (DateTime.UtcNow - _holdStart).TotalMilliseconds / HoldDuration.TotalMilliseconds;
        var trackWidth = Math.Max(0, Root.ActualWidth - 48);
        HoldBar.Width = Math.Clamp(t, 0, 1) * trackWidth;
        if (t >= 1)
        {
            _holding = false;
            _pressed = false;
            _holdTimer.Stop();
            HoldBar.Width = 0;
            UnlockRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CancelHold()
    {
        _holding = false;
        _holdTimer.Stop();
        HoldBar.Width = 0;
    }

    private void Toggle()
    {
        if (IsLocked) UnlockRequested?.Invoke(this, EventArgs.Empty);
        else LockRequested?.Invoke(this, EventArgs.Empty);
    }

    private void LockToggle_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is not (global::Windows.System.VirtualKey.Enter or global::Windows.System.VirtualKey.Space)) return;
        e.Handled = true;
        // Locking is always one key press. Unlocking by keyboard is allowed only when the
        // deliberate press-and-hold is switched off; otherwise a stray Enter on the focused
        // pill would defeat the whole point of the hold.
        if (IsLocked && HoldToUnlock) return;
        Toggle();
    }
}
