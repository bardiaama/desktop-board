using DesktopBoard.Core.Interfaces;

namespace DesktopBoard.Core.Services;

public sealed class BoardStateService : IBoardStateService
{
    private bool _isLocked = true;

    public bool IsLocked => _isLocked;

    public event EventHandler<bool>? LockStateChanged;

    public void Lock() => Set(true);
    public void Unlock() => Set(false);
    public void Toggle() => Set(!_isLocked);

    private void Set(bool locked)
    {
        if (_isLocked == locked) return;
        _isLocked = locked;
        LockStateChanged?.Invoke(this, locked);
    }
}
