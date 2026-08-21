using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>Cursor position through <c>GetCursorPos</c>. That is all (SRP).</summary>
public sealed class CursorProvider : ICursorProvider
{
    public PixelPoint GetPosition()
    {
        if (!NativeMethods.GetCursorPos(out POINT point))
        {
            return default;
        }

        return new PixelPoint(point.X, point.Y);
    }
}
