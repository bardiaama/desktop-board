namespace DesktopBoard.Core.Models;

public abstract class Entity
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TaskItem : Entity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskCategory Category { get; set; } = TaskCategory.Today;
    public Section Section { get; set; }
    public bool IsCompleted { get; set; }
    public Priority Priority { get; set; } = Priority.None;
    /// <summary>Date only (local). "This Week" uses it to pick the weekday.</summary>
    public DateOnly? DueDate { get; set; }
    /// <summary>Optional time of day, e.g. 11:00.</summary>
    public TimeOnly? Time { get; set; }
    /// <summary>Optional hex color (#RRGGBB) used as the dot/accent for week items.</summary>
    public string? Color { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Project : Entity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    /// <summary>0-100, or null when progress is not tracked.</summary>
    public int? Progress { get; set; }
    /// <summary>Hex color like #3B82F6.</summary>
    public string Color { get; set; } = "#3B82F6";
    public int SortOrder { get; set; }
}

public sealed class Note : Entity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Section Section { get; set; }
}

public sealed class StickyNote : Entity
{
    public string Content { get; set; } = string.Empty;
    public Section Section { get; set; }
    /// <summary>One of the sticky palette names: yellow, pink, cyan, green, purple.</summary>
    public string Color { get; set; } = "yellow";
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double Width { get; set; } = 150;
    public double Height { get; set; } = 90;
    public double Rotation { get; set; }
}

public sealed class Meeting : Entity
{
    public string Title { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string Color { get; set; } = "#3B82F6";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public sealed class Goal : Entity
{
    public string Title { get; set; } = string.Empty;
    public Section Section { get; set; }
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AppSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
}

/// <summary>Everything on the board. Used for backup/export and, later, sync.</summary>
public sealed class BoardSnapshot
{
    public int FormatVersion { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<TaskItem> Tasks { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public List<StickyNote> StickyNotes { get; set; } = new();
    public List<Meeting> Meetings { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<AppSetting> Settings { get; set; } = new();
}
