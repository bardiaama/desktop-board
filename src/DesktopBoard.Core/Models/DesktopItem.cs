namespace DesktopBoard.Core.Models;

/// <summary>
/// One entry of the Windows desktop (a shortcut, file, folder or the Recycle Bin), with its
/// icon already rasterized so the UI layer needs no shell knowledge.
/// </summary>
public sealed class DesktopItem
{
    /// <summary>Display name without the .lnk extension.</summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>File-system path, or a shell parsing name (::{GUID}) for virtual items.</summary>
    public string Path { get; init; } = string.Empty;
    public bool IsVirtual { get; init; }
    public bool IsFolder { get; init; }
    /// <summary>Premultiplied BGRA pixels, top-down; null when no icon could be produced.</summary>
    public byte[]? IconBgra { get; init; }
    public int IconWidth { get; init; }
    public int IconHeight { get; init; }
}
