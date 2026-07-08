namespace DeezFuelGauge.Services;

public sealed class DebouncedAction : IDisposable
{
    private readonly Action _action;
    private readonly TimeSpan _delay;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    public DebouncedAction(Action action, TimeSpan delay)
    {
        _action = action;
        _delay = delay;
    }

    public void Invoke()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _ = RunDelayedAsync(token);
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        _action();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task RunDelayedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(_delay, token);
            _action();
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer invoke
        }
    }
}
