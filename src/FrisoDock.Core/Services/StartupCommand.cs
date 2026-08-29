namespace FrisoDock.Core.Services;

/// <summary>
/// The command line the Windows startup entry carries, and whether it launches a given
/// executable. A pure function over strings, therefore testable without a registry.
///
/// The comparison exists because the entry survives the executable moving. A dock that was
/// registered from one folder and now runs from another would still find a value under its name
/// and report itself as starting with Windows, while the logon launches a path that is no longer
/// there. The question the settings screen has to answer is not "is something registered", it is
/// "is this copy registered".
/// </summary>
public static class StartupCommand
{
    /// <summary>
    /// The command for an executable. Quoted, because the path can carry spaces and the shell
    /// splits the command line on them.
    /// </summary>
    public static string For(string executablePath) => $"\"{executablePath}\"";

    /// <summary>
    /// True when the entry launches this very executable, comparing the full paths so a folder
    /// written differently is not read as a different install.
    ///
    /// An entry carrying arguments reads as somebody else's: the dock never writes one, and
    /// answering false only costs a rewrite the next time the user turns the option on.
    /// </summary>
    public static bool PointsAt(string? command, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        string registered = command.Trim();

        // Unquote only a fully quoted command; anything after the closing quote is an argument.
        if (registered.Length > 1 && registered[0] == '"')
        {
            if (!registered.EndsWith('"'))
            {
                return false;
            }

            registered = registered[1..^1];
        }

        if (registered.Length == 0)
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(registered),
                Path.GetFullPath(executablePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // A path Windows would not launch either. Not ours.
            return false;
        }
    }
}
