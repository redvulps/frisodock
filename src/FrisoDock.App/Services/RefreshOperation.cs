using System.Diagnostics;

namespace FrisoDock.App.Services;

/// <summary>Runs one dispatcher-owned refresh at a time and isolates a source's failures.</summary>
internal sealed class RefreshOperation<T>
{
    private readonly string _name;
    private bool _running;

    public RefreshOperation(string name)
    {
        _name = name;
    }

    public async Task RunAsync(Func<Task<T>> read, Action<T> apply)
    {
        if (_running)
        {
            return;
        }

        _running = true;
        try
        {
            T value = await read();
            apply(value);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Trace.TraceError("{0} refresh failed: {1}", _name, exception);
        }
        finally
        {
            _running = false;
        }
    }
}
