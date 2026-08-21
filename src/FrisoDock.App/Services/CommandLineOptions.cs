namespace FrisoDock.App.Services;

/// <summary>
/// Parses the command line arguments. That is all (SRP).
///
/// The flags exist for diagnostics: they allow running the dock without changing the user's
/// environment, which is indispensable for testing on a working machine.
/// </summary>
public sealed class CommandLineOptions
{
    private const string RestoreTaskbarFlag = "--restore-taskbar";
    private const string KeepTaskbarFlag = "--keep-taskbar";
    private const string NoReserveFlag = "--no-reserve";

    private CommandLineOptions(bool restoreTaskbarAndExit, bool keepTaskbar, bool reserveScreenSpace)
    {
        RestoreTaskbarAndExit = restoreTaskbarAndExit;
        KeepTaskbar = keepTaskbar;
        ReserveScreenSpace = reserveScreenSpace;
    }

    /// <summary>
    /// Rescue mode: restores the taskbar and exits. For when a previous instance
    /// died abnormally and left the taskbar hidden.
    /// </summary>
    public bool RestoreTaskbarAndExit { get; }

    /// <summary>Does not hide the native taskbar. Dock and taskbar coexist — useful for debugging.</summary>
    public bool KeepTaskbar { get; }

    /// <summary>Registers the appbar and reserves screen space.</summary>
    public bool ReserveScreenSpace { get; }

    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        return new CommandLineOptions(
            restoreTaskbarAndExit: HasFlag(args, RestoreTaskbarFlag),
            keepTaskbar: HasFlag(args, KeepTaskbarFlag),
            reserveScreenSpace: !HasFlag(args, NoReserveFlag));
    }

    /// <summary>Projects the flags onto the dock's default configuration.</summary>
    public DockSettings ApplyTo(DockSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new DockSettings
        {
            Edge = settings.Edge,
            Metrics = settings.Metrics,
            RefreshDebounceMilliseconds = settings.RefreshDebounceMilliseconds,
            IconExtractionSize = settings.IconExtractionSize,
            HideNativeTaskbar = settings.HideNativeTaskbar && !KeepTaskbar,
            ReserveScreenSpace = settings.ReserveScreenSpace && ReserveScreenSpace,
        };
    }

    private static bool HasFlag(IReadOnlyList<string> args, string flag)
    {
        return args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
    }
}
