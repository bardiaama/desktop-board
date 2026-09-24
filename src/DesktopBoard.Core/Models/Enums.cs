namespace DesktopBoard.Core.Models;

/// <summary>Which half of the board an item belongs to.</summary>
public enum Section
{
    Work = 0,
    Personal = 1
}

/// <summary>Which task card an item is shown in.</summary>
public enum TaskCategory
{
    Today = 0,
    ThisWeek = 1,
    Future = 2
}

public enum Priority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public enum ProjectStatus
{
    Active = 0,
    OnHold = 1,
    Done = 2
}
