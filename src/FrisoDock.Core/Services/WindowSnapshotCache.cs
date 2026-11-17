using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>Shares one window enumeration across dock monitors until a WinEvent invalidates it.</summary>
public sealed class WindowSnapshotCache : IWindowEnumerator, IDisposable
{
    private readonly IWindowEnumerator _source;
    private readonly BoundedCache<byte, IReadOnlyList<WindowInfo>> _cache = new(1);

    public WindowSnapshotCache(IWindowEnumerator source)
    {
        _source = source;
        _source.WindowsChanged += OnWindowsChanged;
    }

    public event EventHandler? WindowsChanged;

    public IReadOnlyList<WindowInfo> GetWindows() => _cache.GetOrAdd(0, _source.GetWindows);

    public void Start()
    {
        _source.Start();
    }

    public void Stop()
    {
        _source.Stop();
        _cache.Clear();
    }

    public void Dispose()
    {
        _source.WindowsChanged -= OnWindowsChanged;
        Stop();
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        _cache.Clear();
        WindowsChanged?.Invoke(this, e);
    }
}
