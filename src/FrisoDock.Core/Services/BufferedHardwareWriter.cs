namespace FrisoDock.Core.Services;

/// <summary>Bounds pending hardware writes to one merged request and preserves their order.</summary>
internal sealed class BufferedHardwareWriter<T> where T : struct
{
    private readonly Func<T, Task> _write;
    private readonly Func<T, T, T> _merge;
    private readonly int _interval;
    private readonly object _gate = new();
    private T? _pending;
    private TaskCompletionSource? _completion;
    private long _revision;

    public BufferedHardwareWriter(Func<T, Task> write, Func<T, T, T> merge, int interval)
    {
        _write = write;
        _merge = merge;
        _interval = interval;
    }

    public Task Enqueue(T value)
    {
        lock (_gate)
        {
            _revision++;
            _pending = _pending is T pending ? _merge(pending, value) : value;
            if (_completion is null)
            {
                _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = DrainAsync(_completion);
            }

            return _completion.Task;
        }
    }

    public async Task<TResult> ReadAsync<TResult>(Func<Task<TResult>> read)
    {
        while (true)
        {
            Task pending;
            long revision;
            lock (_gate)
            {
                pending = _completion?.Task ?? Task.CompletedTask;
                revision = _revision;
            }

            try
            {
                await pending.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A rejected command must not prevent reading the device's actual state.
            }

            TResult value = await read().ConfigureAwait(false);
            lock (_gate)
            {
                // Another panel may have submitted input while this sample was being read.
                if (revision == _revision)
                {
                    return value;
                }
            }
        }
    }

    private async Task DrainAsync(TaskCompletionSource completion)
    {
        Exception? failure = null;
        while (true)
        {
            await Task.Delay(_interval).ConfigureAwait(false);
            T value;
            lock (_gate)
            {
                value = _pending!.Value;
                _pending = null;
            }

            try
            {
                await _write(value).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = exception;
            }

            lock (_gate)
            {
                if (_pending is not null)
                {
                    continue;
                }

                _completion = null;
                if (failure is null)
                {
                    completion.SetResult();
                }
                else
                {
                    completion.SetException(failure);
                }

                return;
            }
        }
    }
}
