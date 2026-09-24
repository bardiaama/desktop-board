namespace DesktopBoard.Core.Services;

/// <summary>
/// Coalesces rapid calls (e.g. keystrokes) into a single action that runs after
/// <see cref="Delay"/> of inactivity. Thread-safe; the action runs on the thread pool.
/// </summary>
public sealed class Debouncer : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private Func<Task>? _pending;

    public Debouncer(TimeSpan delay) => Delay = delay;

    public TimeSpan Delay { get; }

    /// <summary>True while an action is waiting to run.</summary>
    public bool HasPending
    {
        get { lock (_gate) return _pending is not null; }
    }

    public void Debounce(Func<Task> action)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = cts = new CancellationTokenSource();
            _pending = action;
        }

        _ = RunLater(cts.Token);
    }

    /// <summary>Runs the pending action immediately (if any). Used on shutdown so nothing is lost.</summary>
    public async Task FlushAsync()
    {
        Func<Task>? action;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            action = _pending;
            _pending = null;
        }

        if (action is not null) await action().ConfigureAwait(false);
    }

    private async Task RunLater(CancellationToken token)
    {
        try
        {
            await Task.Delay(Delay, token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        Func<Task>? action;
        lock (_gate)
        {
            if (token.IsCancellationRequested) return;
            action = _pending;
            _pending = null;
        }

        if (action is not null)
        {
            try { await action().ConfigureAwait(false); }
            catch (Exception ex) { Failed?.Invoke(this, ex); }
        }
    }

    public event EventHandler<Exception>? Failed;

    public void Dispose()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _pending = null;
        }
    }
}
