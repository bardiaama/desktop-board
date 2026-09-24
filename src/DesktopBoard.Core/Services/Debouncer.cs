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
    private Task? _running;

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

    /// <summary>
    /// Runs the pending action immediately (if any) and waits for an action that is already
    /// executing, so callers (e.g. app shutdown) know every write has completed.
    /// </summary>
    public async Task FlushAsync()
    {
        Func<Task>? action;
        Task? running;
        lock (_gate)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            action = _pending;
            _pending = null;
            running = _running;
        }

        if (running is not null)
        {
            try { await running.ConfigureAwait(false); } catch { /* already reported via Failed */ }
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

        Task? work = null;
        lock (_gate)
        {
            if (token.IsCancellationRequested) return;
            var action = _pending;
            _pending = null;
            if (action is not null)
            {
                work = Execute(action);
                _running = work;
            }
        }

        if (work is not null)
        {
            await work.ConfigureAwait(false);
            lock (_gate)
            {
                if (ReferenceEquals(_running, work)) _running = null;
            }
        }
    }

    private async Task Execute(Func<Task> action)
    {
        try { await action().ConfigureAwait(false); }
        catch (Exception ex) { Failed?.Invoke(this, ex); }
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
