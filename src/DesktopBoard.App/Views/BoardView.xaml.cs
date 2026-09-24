using System.ComponentModel;
using System.Runtime.CompilerServices;
using DesktopBoard.App.ViewModels;
using DesktopBoard.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace DesktopBoard.App.Views;

/// <summary>The whole board: header, Work column, Personal column.</summary>
public sealed partial class BoardView : UserControl, INotifyPropertyChanged
{
    public BoardView()
    {
        ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
        InitializeComponent();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MainViewModel.IsLocked) or nameof(MainViewModel.HoldToUnlock))
                RaiseLockLabels();
            // When the board locks, pull keyboard focus out of the body so no TextBox keeps the caret.
            if (e.PropertyName == nameof(MainViewModel.IsLocked) && ViewModel.IsLocked)
                Lock.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        };
        ViewModel.Strings.PropertyChanged += (_, _) => RaiseLockLabels();
    }

    public MainViewModel ViewModel { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string LockTitle => ViewModel.IsLocked ? ViewModel.Strings.BoardLocked : ViewModel.Strings.EditBoard;
    public string LockSubtitle => ViewModel.IsLocked ? ViewModel.Strings.BoardLockedFa : ViewModel.Strings.EditBoardFa;
    public string LockHint => ViewModel.IsLocked
        ? (ViewModel.HoldToUnlock ? ViewModel.Strings.HoldToUnlock : ViewModel.Strings.ClickToUnlock)
        : ViewModel.Strings.ClickToLock;

    private void RaiseLockLabels()
    {
        OnPropertyChanged(nameof(LockTitle));
        OnPropertyChanged(nameof(LockSubtitle));
        OnPropertyChanged(nameof(LockHint));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Keyboard navigation must not reach any control on a locked board.</summary>
    private void Body_GettingFocus(Microsoft.UI.Xaml.UIElement sender, Microsoft.UI.Xaml.Input.GettingFocusEventArgs args)
    {
        if (ViewModel.IsLocked && !args.TryCancel())
            args.Handled = true;
    }

    private void Lock_LockRequested(object? sender, EventArgs e) => ViewModel.LockCommand.Execute(null);
    private void Lock_UnlockRequested(object? sender, EventArgs e) => ViewModel.UnlockCommand.Execute(null);

    private void ToggleLock_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.ToggleLockCommand.Execute(null);
        args.Handled = true;
    }

    private void Lock_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.LockCommand.Execute(null);
        args.Handled = true;
    }

    private async void Settings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var dialog = new SettingsDialog { XamlRoot = XamlRoot };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Settings dialog failed", ex);
        }
    }
}
