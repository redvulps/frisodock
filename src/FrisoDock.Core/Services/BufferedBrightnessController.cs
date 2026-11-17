using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>Coalesces slider requests across panels and serializes their final hardware writes.</summary>
public sealed class BufferedBrightnessController : IBrightnessController
{
    private readonly IBrightnessController _source;
    private readonly BufferedHardwareWriter<int> _writer;

    public BufferedBrightnessController(IBrightnessController source)
    {
        _source = source;
        _writer = new BufferedHardwareWriter<int>(source.SetLevelAsync, (_, value) => value, 100);
    }

    public Task<BrightnessStatus> GetStatusAsync() => _writer.ReadAsync(_source.GetStatusAsync);

    public Task SetLevelAsync(int level) => _writer.Enqueue(Math.Clamp(level, 0, 100));
}
