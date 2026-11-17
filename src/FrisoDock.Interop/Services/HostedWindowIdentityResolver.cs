namespace FrisoDock.Interop.Services;

internal readonly record struct WindowProcessIdentity(
    uint ProcessId, string? ExecutablePath, string? DisplayName, string? ApplicationUserModelId = null);

/// <summary>Resolves the app inside an ApplicationFrameHost window without changing its activation HWND.</summary>
internal sealed class HostedWindowIdentityResolver
{
    private readonly Func<nint, uint> _processId;
    private readonly Func<uint, string?> _executablePath;
    private readonly Func<nint, IReadOnlyList<nint>> _children;
    private readonly Func<nint, string> _className;
    private readonly Func<nint, string?> _windowApplicationId;
    private readonly Func<uint, string?> _processApplicationId;
    private readonly Func<IReadOnlyList<nint>> _detachedWindows;

    public HostedWindowIdentityResolver(
        Func<nint, uint> processId,
        Func<uint, string?> executablePath,
        Func<nint, IReadOnlyList<nint>> children,
        Func<nint, string> className,
        Func<nint, string?>? windowApplicationId = null,
        Func<uint, string?>? processApplicationId = null,
        Func<IReadOnlyList<nint>>? detachedWindows = null)
    {
        _processId = processId;
        _executablePath = executablePath;
        _children = children;
        _className = className;
        _windowApplicationId = windowApplicationId ?? (_ => null);
        _processApplicationId = processApplicationId ?? (_ => null);
        _detachedWindows = detachedWindows ?? (() => []);
    }

    public WindowProcessIdentity Resolve(nint window, string title)
    {
        uint frameProcessId = _processId(window);
        string? framePath = _executablePath(frameProcessId);
        if (!IsFrameHost(framePath))
        {
            return new WindowProcessIdentity(frameProcessId, framePath, null);
        }

        if (FindContent(_children(window), frameProcessId, title) is WindowProcessIdentity content)
        {
            return content;
        }

        // Minimizing a UWP frame can detach its CoreWindow from the child tree.
        // Match detached content by the frame's Shell identity, never its title or host PID.
        string? applicationId = _windowApplicationId(window);
        if (!string.IsNullOrWhiteSpace(applicationId)
            && FindContent(_detachedWindows(), frameProcessId, title, applicationId) is WindowProcessIdentity detached)
        {
            return detached;
        }

        return new WindowProcessIdentity(frameProcessId, framePath, title, applicationId);
    }

    private WindowProcessIdentity? FindContent(
        IReadOnlyList<nint> candidates, uint frameProcessId, string title, string? expectedApplicationId = null)
    {
        foreach (nint child in candidates)
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
                string? applicationId = _processApplicationId(processId);
                if (expectedApplicationId is null
                    || string.Equals(expectedApplicationId, applicationId, StringComparison.OrdinalIgnoreCase))
                {
                    return new WindowProcessIdentity(processId, path, title, applicationId);
                }
            }
        }

        return null;
    }

    private static bool IsFrameHost(string? path) => string.Equals(
        Path.GetFileName(path), "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
}
