namespace FrisoDock.Core.Models;

/// <summary>
/// Owner of a HICON. Core cannot call Win32, so whoever creates the handle
/// (the Interop layer) injects the release routine. This way the model stays free of P/Invoke
/// and still guarantees deterministic disposal of the native resource.
/// </summary>
public sealed class IconHandle : IDisposable
{
    private readonly Action<nint>? _release;
    private nint _value;

    public IconHandle(nint value, Action<nint>? release)
    {
        _value = value;
        _release = release;
    }

    /// <summary>Raw HICON. It is zero after disposal.</summary>
    public nint Value => _value;

    public bool IsValid => _value != 0;

    public void Dispose()
    {
        nint handle = Interlocked.Exchange(ref _value, 0);
        if (handle == 0)
        {
            return;
        }

        _release?.Invoke(handle);
    }
}
