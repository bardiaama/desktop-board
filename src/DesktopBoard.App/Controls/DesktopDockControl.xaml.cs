using DesktopBoard.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopBoard.App.Controls;

/// <summary>The desktop-icon dock. Stays interactive even while the board is locked (launching is not editing).</summary>
public sealed partial class DesktopDockControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(DesktopDockViewModel), typeof(DesktopDockControl), new PropertyMetadata(null));

    public DesktopDockControl()
    {
        InitializeComponent();
    }

    public DesktopDockViewModel? ViewModel
    {
        get => (DesktopDockViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Tells the view-model how many icon rows fit, so it can pick the column count.</summary>
    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel is null || e.NewSize.Height <= 0) return;
        ViewModel.RowsThatFit = Math.Max(1, (int)((e.NewSize.Height - 12) / 90));
    }
}
