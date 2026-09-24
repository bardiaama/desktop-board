using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace DesktopBoard.App.Controls;

/// <summary>
/// A glass panel with an icon, a title with a hand-drawn underline, an optional subtitle
/// and an optional header slot (counter, add button). The template lives in Styles/Cards.xaml.
/// </summary>
public sealed class BoardCard : ContentControl
{
    public BoardCard()
    {
        DefaultStyleKey = typeof(BoardCard);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(BoardCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(BoardCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(BoardCard), new PropertyMetadata(""));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(BoardCard), new PropertyMetadata(null));

    public static readonly DependencyProperty AccentSoftProperty =
        DependencyProperty.Register(nameof(AccentSoft), typeof(Brush), typeof(BoardCard), new PropertyMetadata(null));

    public static readonly DependencyProperty UnderlineProperty =
        DependencyProperty.Register(nameof(Underline), typeof(Brush), typeof(BoardCard), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(BoardCard), new PropertyMetadata(null));

    public static readonly DependencyProperty GlassProperty =
        DependencyProperty.Register(nameof(Glass), typeof(bool), typeof(BoardCard), new PropertyMetadata(true, OnGlassChanged));

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    /// <summary>Segoe Fluent Icons glyph.</summary>
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public Brush? AccentSoft { get => (Brush?)GetValue(AccentSoftProperty); set => SetValue(AccentSoftProperty, value); }
    public Brush? Underline { get => (Brush?)GetValue(UnderlineProperty); set => SetValue(UnderlineProperty, value); }
    public object? HeaderContent { get => GetValue(HeaderContentProperty); set => SetValue(HeaderContentProperty, value); }
    /// <summary>True: acrylic background. False: plain translucent panel (cheaper).</summary>
    public bool Glass { get => (bool)GetValue(GlassProperty); set => SetValue(GlassProperty, value); }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateGlassState(false);
    }

    private static void OnGlassChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((BoardCard)d).UpdateGlassState(true);

    private void UpdateGlassState(bool useTransitions) =>
        VisualStateManager.GoToState(this, Glass ? "GlassOn" : "GlassOff", useTransitions);
}
