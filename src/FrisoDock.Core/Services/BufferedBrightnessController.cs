using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>Coalesces slider requests across panels and serializes their final hardware writes.</summary>
public sealed class BufferedBrightnessController : IBrightnessController
{
    private readonly IBrightnessController _source;
    private readonly object _gate = new();
    private int? _pending;
    private TaskCompletionSource? _completion;

    public BufferedBrightnessController(IBrightnessController source)
    {
        _source = source;
    }

    public Task<BrightnessStatus> GetStatusAsync() => _source.GetStatusAsync();

    public Task SetLevelAsync(int level)
    {
        lock (_gate)
        {
            _pending = Math.Clamp(level, 0, 100);
            if (_completion is null)
            {
                _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _ = DrainAsync(_completion);
            }

            return _completion.Task;
        }
    }

    private async Task DrainAsync(TaskCompletionSource completion)
    {
        Exception? failure = null;
        while (true)
        {
            await Task.Delay(100).ConfigureAwait(false);
            int level;
            lock (_gate)
            {
                level = _pending!.Value;
                _pending = null;
            }

            try
            {
                await _source.SetLevelAsync(level).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // A failed write must not drop newer input that arrived while it was running.
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
