using DesktopBoard.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DesktopBoard.App.Controls;

/// <summary>Shared card body for goals and task lists: reorderable rows plus an add line.</summary>
public sealed partial class ChecklistControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ChecklistViewModel), typeof(ChecklistControl), new PropertyMetadata(null));

    public ChecklistControl()
    {
        InitializeComponent();
    }

    public ChecklistViewModel? ViewModel
    {
        get => (ChecklistViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void AddBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || ViewModel is null) return;
        e.Handled = true;
        ViewModel.AddCommand.Execute(null);
        // keep the caret in the add box so several items can be typed in a row
        AddBox.Focus(FocusState.Keyboard);
    }
}
