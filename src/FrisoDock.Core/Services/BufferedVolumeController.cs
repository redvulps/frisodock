using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>Coalesces volume and mute requests across panels without dropping their final values.</summary>
public sealed class BufferedVolumeController : IVolumeController
{
    private readonly IVolumeController _source;
    private readonly BufferedHardwareWriter<Update> _writer;

    public BufferedVolumeController(IVolumeController source)
    {
        _source = source;
        _writer = new BufferedHardwareWriter<Update>(value => source.SetStateAsync(value.Level, value.IsMuted),
            (pending, value) => new Update(value.Level ?? pending.Level, value.IsMuted ?? pending.IsMuted), 50);
    }

    public Task<VolumeStatus> GetStatusAsync() => _writer.ReadAsync(_source.GetStatusAsync);

    public Task SetStateAsync(int? level = null, bool? isMuted = null) => level is null && isMuted is null
        ? Task.CompletedTask
        : _writer.Enqueue(new Update(level is int value ? Math.Clamp(value, 0, 100) : null, isMuted));

    private readonly record struct Update(int? Level, bool? IsMuted);
}
