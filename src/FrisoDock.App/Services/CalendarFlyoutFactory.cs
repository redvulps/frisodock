using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Creates the calendar flyout model. That is all (SRP).
///
/// It exists because the flyout is created on every open — the calendar must show the day of
/// the click, not the day the dock started —, so it cannot come ready from the container.
/// </summary>
public sealed class CalendarFlyoutFactory
{
    private readonly IClock _clock;
    private readonly CalendarMonthBuilder _builder;

    public CalendarFlyoutFactory(IClock clock, CalendarMonthBuilder builder)
    {
        _clock = clock;
        _builder = builder;
    }

    public CalendarFlyoutViewModel Create()
    {
        return new CalendarFlyoutViewModel(_clock.Now, _builder);
    }
}
