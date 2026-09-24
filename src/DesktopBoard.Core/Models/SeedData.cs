namespace DesktopBoard.Core.Models;

/// <summary>
/// Sample content used the first time the database is created. These are ordinary,
/// fully editable records; nothing else in the app special-cases them.
/// </summary>
public static class SeedData
{
    public static BoardSnapshot Create(DateOnly today)
    {
        var s = new BoardSnapshot();
        int order;

        order = 0;
        foreach (var t in new[] { "تکمیل طراحی ماژول GSM", "شروع تست eSIM", "آماده‌سازی گزارش هفتگی", "بررسی همکاری‌های جدید" })
            s.Goals.Add(new Goal { Title = t, Section = Section.Work, IsCompleted = order < 2, SortOrder = order++ });

        order = 0;
        foreach (var t in new[] { "ورزش منظم", "مطالعه روزانه", "بهبود خواب", "مدیریت مالی شخصی", "برنامه‌ریزی سفر" })
            s.Goals.Add(new Goal { Title = t, Section = Section.Personal, SortOrder = order++ });

        order = 0;
        s.Projects.Add(new Project { Name = "GSM Module", Color = "#3B82F6", Status = ProjectStatus.Active, Progress = 70, SortOrder = order++ });
        s.Projects.Add(new Project { Name = "eSIM Integration", Color = "#22C55E", Status = ProjectStatus.Active, Progress = 35, SortOrder = order++ });
        s.Projects.Add(new Project { Name = "Ticketing System", Color = "#EAB308", Status = ProjectStatus.Active, Progress = 50, SortOrder = order++ });
        s.Projects.Add(new Project { Name = "RIMAEX Content", Color = "#A855F7", Status = ProjectStatus.Active, SortOrder = order++ });

        order = 0;
        var workToday = new (string Title, string? Time, bool Done)[]
        {
            ("تماس با مشتری", "11:00", true),
            ("بررسی ایمیل‌ها", "12:00", true),
            ("تکمیل طراحی GSM", "14:00", false),
            ("مطالعه درباره eSIM", "15:30", false),
            ("نوشتن گزارش هفتگی", "17:00", false),
            ("جلسه تیم", "18:30", false),
        };
        foreach (var (title, time, done) in workToday)
            s.Tasks.Add(new TaskItem
            {
                Title = title, Section = Section.Work, Category = TaskCategory.Today, IsCompleted = done,
                Time = time is null ? null : TimeOnly.Parse(time), DueDate = today, SortOrder = order++
            });

        order = 0;
        foreach (var t in new[] { "ورزش عصر", "مطالعه کتاب", "پیگیری کارت اقامت عمان", "بررسی برنامه سفر", "تماس با خانواده", "مرور برنامه هفته" })
            s.Tasks.Add(new TaskItem { Title = t, Section = Section.Personal, Category = TaskCategory.Today, IsCompleted = t == "مطالعه کتاب", DueDate = today, SortOrder = order++ });

        // "This week": one item per weekday starting Saturday (Persian week).
        order = 0;
        var weekStart = StartOfPersianWeek(today);
        var week = new[] { "تمدید اکانت کلود", "بررسی بلیت مسقط", "خرید وسایل سفر", "مراجعه به پزشک", "تمرین زبان آلمانی", "دیدار با دوست", "استراحت" };
        var weekColors = new[] { "#3B82F6", "#EAB308", "#22C55E", "#A855F7", "#EC4899", "#3B82F6", "#9CA3AF" };
        for (var i = 0; i < week.Length; i++)
            s.Tasks.Add(new TaskItem
            {
                Title = week[i], Section = Section.Personal, Category = TaskCategory.ThisWeek,
                DueDate = weekStart.AddDays(i), Color = weekColors[i], SortOrder = order++
            });

        order = 0;
        foreach (var t in new[] { "ثبت‌نام دوره زبان", "برنامه‌ریزی مالی Q2", "بررسی برند جدید" })
            s.Tasks.Add(new TaskItem { Title = t, Section = Section.Personal, Category = TaskCategory.Future, SortOrder = order++ });

        order = 0;
        s.Meetings.Add(new Meeting { Title = "تماس با مشتری", Date = today, Time = new TimeOnly(11, 0), Color = "#3B82F6", SortOrder = order++ });
        s.Meetings.Add(new Meeting { Title = "جلسه طراحی", Date = today, Time = new TimeOnly(14, 0), Color = "#EAB308", SortOrder = order++ });
        s.Meetings.Add(new Meeting { Title = "بررسی مستندات", Date = today, Time = new TimeOnly(16, 30), Color = "#22C55E", SortOrder = order++ });

        s.Notes.Add(new Note
        {
            Title = "یادداشت سریع", Section = Section.Work,
            Content = "• بررسی امکان استفاده از eSIM\n• مقایسه ماژول‌ها در بازار\n• آماده‌سازی سوالات فنی"
        });
        s.Notes.Add(new Note
        {
            Title = "یادداشت‌ها", Section = Section.Personal,
            Content = "مدارک سفر:\n• پاسپورت\n• بلیت\n• بیمه مسافرتی\n• رزرو اقامت\n\nخرید:\n☐ شارژر\n☐ لباس گرم\n☐ کارت حافظه"
        });

        s.StickyNotes.Add(new StickyNote { Content = "MVP\nنسخه اول", Section = Section.Work, Color = "yellow", PositionX = 8, PositionY = 6, Rotation = -2 });
        s.StickyNotes.Add(new StickyNote { Content = "اتصال به\nGoogle Calendar", Section = Section.Work, Color = "pink", PositionX = 172, PositionY = 4, Rotation = 1.5 });
        s.StickyNotes.Add(new StickyNote { Content = "طراحی اپ موبایل\nیکپارچه", Section = Section.Work, Color = "cyan", PositionX = 336, PositionY = 8, Rotation = -1 });
        s.StickyNotes.Add(new StickyNote { Content = "بررسی بازار\nخارجی", Section = Section.Work, Color = "green", PositionX = 500, PositionY = 5, Rotation = 2 });

        s.StickyNotes.Add(new StickyNote { Content = "سفر مسقط\n(بررسی مسیر)", Section = Section.Personal, Color = "pink", PositionX = 8, PositionY = 6, Rotation = -1.5 });
        s.StickyNotes.Add(new StickyNote { Content = "خرید نمایشگاه\nالکامپ", Section = Section.Personal, Color = "cyan", PositionX = 172, PositionY = 4, Rotation = 1 });
        s.StickyNotes.Add(new StickyNote { Content = "راه‌اندازی\nپروژه جدید", Section = Section.Personal, Color = "yellow", PositionX = 336, PositionY = 8, Rotation = -2 });

        return s;
    }

    /// <summary>The Persian week starts on Saturday.</summary>
    public static DateOnly StartOfPersianWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
        return date.AddDays(-diff);
    }
}
