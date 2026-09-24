using CommunityToolkit.Mvvm.ComponentModel;
using DesktopBoard.Core.Interfaces;
using DesktopBoard.Core.Models;

namespace DesktopBoard.App.Services;

/// <summary>
/// All user-facing labels. Persian is the primary language; the "bilingual" mode adds the
/// English label as a subtitle where the layout has room. Switching language replaces the
/// values in place so every binding updates.
/// </summary>
public sealed partial class UiStrings : ObservableObject
{
    private readonly ISettingsService _settings;

    private readonly Microsoft.UI.Dispatching.DispatcherQueue? _dispatcher;

    public UiStrings(ISettingsService settings)
    {
        _settings = settings;
        _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Language = settings.GetString(SettingKeys.Language, "bilingual") ?? "bilingual";
        // SettingChanged is raised on a thread-pool thread; bindings must be updated on the UI thread.
        settings.SettingChanged += (_, key) =>
        {
            if (key != SettingKeys.Language) return;
            void Apply() => Language = settings.GetString(SettingKeys.Language, "bilingual") ?? "bilingual";
            if (_dispatcher is null || _dispatcher.HasThreadAccess) Apply();
            else _dispatcher.TryEnqueue(Apply);
        };
    }

    /// <summary>Re-reads the language setting on the UI thread (after a backup import).</summary>
    public void Reload() => Language = _settings.GetString(SettingKeys.Language, "bilingual") ?? "bilingual";

    [ObservableProperty] private string _language = "bilingual";

    partial void OnLanguageChanged(string value) => OnPropertyChanged(string.Empty);

    private bool Fa => Language != "en";
    private bool En => Language != "fa";

    private string T(string fa, string en) => Language switch { "fa" => fa, "en" => en, _ => fa };
    private string Sub(string en) => Language == "bilingual" ? en : string.Empty;

    // Sections
    public string Work => T("کاری", "Work");
    public string WorkSub => Sub("Work");
    public string Personal => T("شخصی", "Personal");
    public string PersonalSub => Sub("Personal");

    // Cards
    public string WorkGoals => T("اهداف کاری", "Work Goals");
    public string WorkGoalsSub => Sub("Work Goals");
    public string ActiveProjects => T("پروژه‌های فعال", "Active Projects");
    public string ActiveProjectsSub => Sub("Projects");
    public string TodayTasks => T("کارهای امروز", "Today's Tasks");
    public string TodayTasksSub => Sub("Today");
    public string Meetings => T("جلسات / قرارها", "Meetings");
    public string MeetingsSub => Sub("Meetings");
    public string QuickNotes => T("یادداشت سریع", "Quick Notes");
    public string QuickNotesSub => Sub("Quick Notes");
    public string Ideas => T("ایده‌ها", "Ideas");
    public string IdeasSub => Sub("Ideas");
    public string PersonalGoals => T("اهداف شخصی", "Personal Goals");
    public string PersonalGoalsSub => Sub("Personal Goals");
    public string ThisWeek => T("برنامه این هفته", "This Week");
    public string ThisWeekSub => Sub("This Week");
    public string Notes => T("یادداشت‌ها", "Notes");
    public string NotesSub => Sub("Notes");
    public string FutureTasks => T("کارهای آینده", "Future Tasks");
    public string FutureTasksSub => Sub("Future");
    public string IdeasPlans => T("ایده‌ها / برنامه‌ها", "Ideas / Plans");
    public string IdeasPlansSub => Sub("Ideas / Plans");

    // Lock control
    public string BoardLocked => T("Board Locked", "Board Locked");
    public string BoardLockedFa => T("قفل تخته", "Locked");
    public string EditBoard => T("Edit Board", "Edit Board");
    public string EditBoardFa => T("حالت ویرایش", "Editing");
    public string HoldToUnlock => T("برای ویرایش، نگه دارید", "Hold to unlock");
    public string ClickToUnlock => T("برای ویرایش، قفل را باز کنید", "Click to unlock");
    public string ClickToLock => T("برای قفل کردن کلیک کنید", "Click to lock");

    // Placeholders / actions
    public string AddGoal => T("افزودن هدف جدید …", "Add a goal …");
    public string AddTask => T("افزودن کار جدید …", "Add a task …");
    public string AddProject => T("افزودن پروژه …", "Add a project …");
    public string AddMeeting => T("افزودن جلسه", "Add meeting");
    public string AddIdea => T("ایده جدید", "New idea");
    public string NotePlaceholder => T("اینجا بنویسید …", "Write here …");
    public string Delete => T("حذف", "Delete");
    public string Settings => T("تنظیمات", "Settings");
    public string NewMeeting => T("جلسه جدید", "New meeting");
    public string NewProject => T("پروژه جدید", "New project");
    public string NewIdea => T("ایده جدید", "New idea");
    public string Details => T("جزئیات", "Details");
    public string Description => T("توضیحات", "Description");
    public string Status => T("وضعیت", "Status");
    public string Progress => T("پیشرفت", "Progress");
    public string StatusActive => T("فعال", "Active");
    public string StatusOnHold => T("متوقف", "On hold");
    public string StatusDone => T("انجام شده", "Done");

    public string StatusLabel(ProjectStatus s) => s switch
    {
        ProjectStatus.OnHold => StatusOnHold,
        ProjectStatus.Done => StatusDone,
        _ => StatusActive
    };

    // Settings dialog
    public string SettingsTitle => T("تنظیمات تخته", "Board Settings");
    public string StartWithWindows => T("اجرا همراه با ویندوز", "Start with Windows");
    public string BackgroundImage => T("تصویر پس‌زمینه", "Background image");
    public string ChooseImage => T("انتخاب تصویر …", "Choose image …");
    public string UseWindowsWallpaper => T("استفاده از والپیپر ویندوز", "Use Windows wallpaper");
    public string Darkness => T("تیرگی پس‌زمینه", "Background darkness");
    public string Blur => T("میزان محو شدن", "Blur intensity");
    public string UiScale => T("مقیاس رابط کاربری", "UI scale");
    public string LanguageLabel => T("زبان", "Language");
    public string LangBilingual => T("فارسی + English", "Persian + English");
    public string LangFa => T("فارسی", "Persian");
    public string LangEn => T("English", "English");
    public string DefaultLocked => T("شروع در حالت قفل", "Start locked");
    public string HoldToUnlockSetting => T("نگه‌داشتن برای باز کردن قفل", "Hold to unlock");
    public string GlassEffect => T("افکت شیشه‌ای کارت‌ها", "Glass effect on cards");
    public string InsetLeft => T("فاصله از آیکون‌های دسکتاپ (چپ)", "Left inset for desktop icons");
    public string DesktopMode => T("حالت نمایش روی دسکتاپ", "Desktop mode");
    public string ModeAuto => T("خودکار", "Automatic");
    public string ModeEmbedded => T("داخل دسکتاپ (پشت آیکون‌ها)", "Embedded (behind icons)");
    public string ModeBottomMost => T("زیر همه پنجره‌ها", "Bottom-most window");
    public string ModeNormal => T("پنجره عادی", "Normal window");
    public string ResetLayout => T("بازنشانی چیدمان", "Reset layout");
    public string ExportBackup => T("خروجی پشتیبان …", "Export backup …");
    public string ImportBackup => T("بازیابی پشتیبان …", "Import backup …");
    public string ImportConfirm => T("همه اطلاعات فعلی تخته با محتوای این فایل جایگزین می‌شود. ادامه می‌دهید؟", "Everything on the board will be replaced by this file. Continue?");
    public string Close => T("بستن", "Close");
    public string ExitApp => T("خروج از Desktop Board", "Exit Desktop Board");
    public string RestartHint => T("تغییر حالت دسکتاپ بعد از اجرای مجدد اعمال می‌شود.", "Desktop mode changes apply after restart.");
    public string BackupDone => T("پشتیبان ذخیره شد.", "Backup saved.");
    public string RestoreDone => T("اطلاعات بازیابی شد.", "Data restored.");
    public string OnboardingTitle => T("به Desktop Board خوش آمدید", "Welcome to Desktop Board");
    public string OnboardingBody => T("می‌خواهید تخته هر بار با ویندوز اجرا شود؟ این گزینه را بعداً در تنظیمات می‌توانید تغییر دهید.", "Start the board automatically with Windows? You can change this later in Settings.");
    public string Yes => T("بله", "Yes");
    public string NotNow => T("فعلاً نه", "Not now");
    public string Error => T("خطا", "Error");
    public string DataFolder => T("پوشه داده‌ها", "Data folder");

    public string UiCulture => Fa ? "fa-IR" : "en-US";
}
