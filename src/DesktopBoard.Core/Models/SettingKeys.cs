namespace DesktopBoard.Core.Models;

/// <summary>Well-known keys in the AppSettings table.</summary>
public static class SettingKeys
{
    public const string SchemaSeeded = "db.seeded";
    public const string StartWithWindows = "startup.enabled";
    public const string OnboardingDone = "onboarding.done";
    public const string BackgroundImagePath = "wallpaper.path";
    public const string BackgroundDarkness = "wallpaper.darkness";   // 0..1
    public const string BackgroundBlur = "wallpaper.blur";           // 0..1
    public const string UiScale = "ui.scale";                        // 0.75..1.5
    public const string Language = "ui.language";                    // "bilingual" | "fa" | "en"
    public const string DefaultLocked = "board.defaultLocked";       // true/false
    public const string HoldToUnlock = "board.holdToUnlock";         // true/false
    public const string DesktopHostMode = "desktop.hostMode";        // "auto" | "embedded" | "bottommost" | "normal"
    public const string GlassEnabled = "ui.glass";                   // true/false (acrylic on cards)
    public const string BoardInsetLeft = "board.insetLeft";          // px kept free on the left for desktop icons
}
