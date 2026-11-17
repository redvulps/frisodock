namespace FrisoDock.Core.Services;

/// <summary>
/// What the Windows "Startup apps" screen recorded about a startup entry. A pure function over
/// the blob it writes, therefore testable without a registry.
///
/// It exists because disabling an app in that screen does not delete the entry: Windows keeps the
/// entry where it was and writes the state next to it, under
/// <c>Explorer\StartupApproved\Run</c>. Reading only the entry would leave the dock's switch on
/// while Windows keeps the dock from starting, which is the worst of the two states.
///
/// The blob is twelve bytes: the state, then a FILETIME of when it was disabled, zeroed while it
/// is enabled. Measured on this machine, OneDrive enabled reads
/// <c>02 00 00 00 00 00 00 00 00 00 00 00</c> and Steam disabled reads
/// <c>03 00 00 00 61 DE 90 B0 24 BA DC 01</c>.
/// </summary>
public static class StartupApproval
{
    /// <summary>Length of the blob Windows writes. Only the first byte is read here.</summary>
    private const int ValueLength = 12;

    /// <summary>The state byte carries the disable flag in bit 0: 2 and 6 are enabled, 3 and 7 are not.</summary>
    private const byte DisabledBit = 1;

    /// <summary>The state byte of an entry the user has not disabled.</summary>
    private const byte EnabledState = 2;

    /// <summary>
    /// True when Windows is not holding the entry back. A missing value means nobody ever
    /// disabled it, which is the normal case: Windows only writes here on the first toggle.
    /// </summary>
    public static bool IsApproved(byte[]? value)
    {
        // A truncated blob still carries the state byte; only an absent or empty one says nothing.
        if (value is null || value.Length == 0)
        {
            return true;
        }

        return (value[0] & DisabledBit) == 0;
    }

    /// <summary>
    /// The blob that clears a previous disable. Writing it is not optional when the user turns
    /// the option back on: leaving the old value behind would make the switch say yes while
    /// Windows still skips the dock at the next logon.
    /// </summary>
    public static byte[] Approved()
    {
        var value = new byte[ValueLength];
        value[0] = EnabledState;

        return value;
    }
}
