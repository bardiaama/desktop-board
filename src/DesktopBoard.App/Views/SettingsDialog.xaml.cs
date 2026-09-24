using DesktopBoard.App.ViewModels;
using DesktopBoard.Core.Services;
using DesktopBoard.Windows.NativeInterop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopBoard.App.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; }

    public FlowDirection Flow => ViewModel.Strings.Language == "en" ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

    private static nint OwnerHwnd => App.Current.MainWindow?.Hwnd ?? nint.Zero;

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var path = FileDialogs.Open(OwnerHwnd, ViewModel.Strings.ChooseImage,
            "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.jfif|All files|*.*",
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        if (path is not null) ViewModel.BackgroundImagePath = path;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var suggested = $"DesktopBoard-backup-{DateTime.Now:yyyy-MM-dd}.json";
        var path = FileDialogs.Save(OwnerHwnd, ViewModel.Strings.ExportBackup, "Desktop Board backup|*.json", "json", suggested);
        if (path is not null) await ViewModel.ExportAsync(path);
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = FileDialogs.Open(OwnerHwnd, ViewModel.Strings.ImportBackup, "Desktop Board backup|*.json|All files|*.*");
        if (path is null) return;

        // Import replaces everything on the board: ask once.
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.Strings.ImportBackup,
            Content = ViewModel.Strings.ImportConfirm + Environment.NewLine + Path.GetFileName(path),
            PrimaryButtonText = ViewModel.Strings.Yes,
            CloseButtonText = ViewModel.Strings.NotNow,
            DefaultButton = ContentDialogButton.Close,
            FlowDirection = Flow
        };
        Hide();
        var result = await confirm.ShowAsync();
        if (result == ContentDialogResult.Primary) await ViewModel.ImportAsync(path);
        await ShowAsync();
    }

    private async void Exit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await App.Current.Services.GetRequiredService<MainViewModel>().FlushAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Flush on exit failed", ex);
        }
        Hide();
        App.Current.MainWindow?.Close();
    }
}
