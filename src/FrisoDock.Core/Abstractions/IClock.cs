namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: telling the time.
///
/// It exists as an abstraction so the dock clock can be tested without depending on the clock of
/// the machine running the tests.
/// </summary>
public interface IClock
{
    /// <summary>Current instant, with the local offset.</summary>
    DateTimeOffset Now { get; }
}

/// <summary>System clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
