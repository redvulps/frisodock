namespace FrisoDock.Core.Models;

/// <summary>
/// Stable identity of an application in the dock. It serves to group several windows
/// under the same icon and to match a running app with a pinned one.
/// </summary>
public readonly record struct AppKey
{
    private AppKey(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    /// <summary>Derives the key from the executable path (case-insensitive).</summary>
    public static AppKey FromExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return default;
        }

        return new AppKey(executablePath.Trim().ToLowerInvariant());
    }

    /// <summary>Derives the key from an AUMID (packaged apps / Start shortcuts).</summary>
    public static AppKey FromApplicationUserModelId(string? aumid)
    {
        if (string.IsNullOrWhiteSpace(aumid))
        {
            return default;
        }

        return new AppKey("aumid:" + aumid.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value ?? string.Empty;
}
