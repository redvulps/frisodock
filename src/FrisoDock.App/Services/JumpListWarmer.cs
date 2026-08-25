using System.Collections.Concurrent;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Warms an app's jump list off the UI thread. That is all (SRP): what to warm belongs to
/// <see cref="JumpListFlyoutFactory"/>.
///
/// It exists because opening a cold jump list is expensive and the cost falls entirely on the UI
/// thread: tens of milliseconds sweeping the destinations folder, plus tens to hundreds extracting
/// each entry's icon (measured: 26 ms + 77 ms on Brave, 71 ms + 188 ms on VS Code, with documents
/// accounting for nearly all of it — a PDF reaches 20 ms on its own).
///
/// The trigger is the cursor resting on the icon. There is no way to right-click without passing
/// there first, so warming always gains something, and whoever never goes near an
/// icon never pays for it. Warming the whole dock at startup would be the opposite: I/O up front
/// for apps the user may never even use.
/// </summary>
public sealed class JumpListWarmer
{
    private readonly JumpListFlyoutFactory _factory;

    /// <summary>
    /// Executables currently being warmed. Without this, sweeping the dock back and forth would fire
    /// the same folder scan several times in parallel.
    ///
    /// It is "warming", not "warmed": what remembers what was read are the caches underneath, and that
    /// is what makes warming happen again after the watcher drops them.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.OrdinalIgnoreCase);

    public JumpListWarmer(JumpListFlyoutFactory factory)
    {
        _factory = factory;
    }

    public void Warm(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.JumpListExecutable is not string executable)
        {
            return;
        }

        if (!_inFlight.TryAdd(executable, 0))
        {
            return;
        }

        Task.Run(() =>
        {
            // Warming is a head start, not an obligation: an unreachable folder or a corrupt icon
            // must not kill the dock from a pool thread, where the exception would have
            // nobody to handle it. The next right-click simply pays the full price.
            try
            {
                _factory.Preload(item);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
            }
            finally
            {
                _inFlight.TryRemove(executable, out _);
            }
        });
    }
}
