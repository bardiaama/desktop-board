using DesktopBoard.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace DesktopBoard.App.Controls;

/// <summary>Free-positioned sticky notes. Dragging is a XAML manipulation; positions persist on release.</summary>
public sealed partial class StickyBoardControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(StickyBoardViewModel), typeof(StickyBoardControl), new PropertyMetadata(null, OnViewModelChanged));

    public StickyBoardControl()
    {
        InitializeComponent();
    }

    public StickyBoardViewModel? ViewModel
    {
        get => (StickyBoardViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (StickyBoardControl)d;
        c.ReportSize();
    }

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e) => ReportSize();

    private void ReportSize()
    {
        if (ViewModel is null || Root.ActualWidth <= 0) return;
        ViewModel.CanvasWidth = Root.ActualWidth;
        ViewModel.CanvasHeight = Root.ActualHeight;
        ViewModel.ClampToCanvas();
    }

    private static StickyNoteViewModel? VmOf(object sender) =>
        sender is FrameworkElement fe ? fe.Tag as StickyNoteViewModel ?? fe.DataContext as StickyNoteViewModel : null;

    private int _zTop;

    /// <summary>The note being dragged comes to the front and stays there.</summary>
    private void Sticky_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && VisualTreeHelper.GetParent(fe) is UIElement container)
            Canvas.SetZIndex(container, ++_zTop);
    }

    private void Sticky_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm && vm.Owner.IsEditMode)
        {
            vm.MoveBy(e.Delta.Translation.X, e.Delta.Translation.Y, Root.ActualWidth, Root.ActualHeight);
            e.Handled = true;
        }
    }

    private void Sticky_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (VmOf(sender) is { } vm)
        {
            vm.CommitPosition();
            e.Handled = true;
        }
    }
}
