namespace FrisoDock.Interop.Services;

internal readonly record struct WindowProcessIdentity(uint ProcessId, string? ExecutablePath, string? DisplayName);

/// <summary>Resolves the app inside an ApplicationFrameHost window without changing its activation HWND.</summary>
internal sealed class HostedWindowIdentityResolver
{
    private readonly Func<nint, uint> _processId;
    private readonly Func<uint, string?> _executablePath;
    private readonly Func<nint, IReadOnlyList<nint>> _children;
    private readonly Func<nint, string> _className;

    public HostedWindowIdentityResolver(
        Func<nint, uint> processId,
        Func<uint, string?> executablePath,
        Func<nint, IReadOnlyList<nint>> children,
        Func<nint, string> className)
    {
        _processId = processId;
        _executablePath = executablePath;
        _children = children;
        _className = className;
    }

    public WindowProcessIdentity Resolve(nint window, string title)
    {
        uint frameProcessId = _processId(window);
        string? framePath = _executablePath(frameProcessId);
        if (!IsFrameHost(framePath))
        {
            return new WindowProcessIdentity(frameProcessId, framePath, null);
        }

        foreach (nint child in _children(window))
        {
            // The content's CoreWindow belongs to the UWP app. Other descendants can belong
            // to the frame or input services and must not become the application's identity.
            if (_className(child) != "Windows.UI.Core.CoreWindow")
            {
                continue;
            }

            uint processId = _processId(child);
            if (processId == 0 || processId == frameProcessId)
            {
                continue;
            }

            string? path = _executablePath(processId);
            if (!string.IsNullOrWhiteSpace(path) && !IsFrameHost(path))
            {
                return new WindowProcessIdentity(processId, path, title);
            }
        }

        // Even while the content process is starting or inaccessible, never label the
        // user's Calculator or Settings window with the host's internal product name.
        return new WindowProcessIdentity(frameProcessId, framePath, title);
    }

    private static bool IsFrameHost(string? path) => string.Equals(
        Path.GetFileName(path), "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
}
