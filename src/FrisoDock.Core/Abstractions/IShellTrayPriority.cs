namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Yields to Explorer, for an instant, the role of first <c>Shell_TrayWnd</c> window.
/// Single responsibility.
///
/// It exists because of a side effect of hosting the tray. The dock creates a window of that class
/// and keeps it ahead of Explorer's, because the first of them is what receives the icons. Except
/// that <c>SHAppBarMessage</c> looks for that same window: the appbar messages the dock itself
/// sends — registering, reserving the band at the edge, hiding the taskbar — reach its own window
/// and not Explorer's, and forwarding does not save them. The ones carrying a rectangle come back
/// successful and reserve nothing, which is worse than failing.
///
/// While the role is yielded, a newly registered icon would go to Explorer. The window is
/// milliseconds long and only opens during an appbar operation, which is rare — and the host's
/// periodic maintenance reasserts the position anyway.
/// </summary>
public interface IShellTrayPriority
{
    /// <summary>
    /// Puts Explorer's window in front until the returned value is disposed. With no tray host
    /// active there is nothing to yield, and the returned value does nothing.
    /// </summary>
    IDisposable Yield();
}

/// <summary>
/// For whoever runs with no tray host — rescue mode, which only restores the taskbar and exits.
/// With no host, Explorer already is the first <c>Shell_TrayWnd</c> and there is no turn to yield.
/// </summary>
public sealed class NoShellTrayPriority : IShellTrayPriority
{
    private sealed class NothingToRelease : IDisposable
    {
        internal static readonly IDisposable Instance = new NothingToRelease();

        public void Dispose()
        {
        }
    }

    public IDisposable Yield()
    {
        return NothingToRelease.Instance;
    }
}
