using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// The calendar flyout: the header with today's date and the month being displayed. That is all
/// (SRP): building the grid belongs to <see cref="CalendarMonthBuilder"/>.
///
/// There is no timer here on purpose. The flyout is transient — it closes on the first click
/// outside — and the only thing that could change under it is the day rolling over at midnight,
/// which the next opening picks up.
/// </summary>
public sealed partial class CalendarFlyoutViewModel : ObservableObject
{
    private readonly CalendarMonthBuilder _builder;
    private readonly DateOnly _today;

    /// <summary>First day of the month being displayed; navigation moves it whole months.</summary>
    private DateOnly _displayedMonth;

    [ObservableProperty]
    private CalendarMonth _month;

    public CalendarFlyoutViewModel(DateTimeOffset now, CalendarMonthBuilder builder)
    {
        _builder = builder;
        _today = DateOnly.FromDateTime(now.Date);
        _displayedMonth = new DateOnly(_today.Year, _today.Month, 1);

        Header = _builder.FormatHeader(now);
        _month = _builder.Build(_displayedMonth, _today);
    }

    /// <summary>Today's date in full, like the native flyout header. It does not navigate along.</summary>
    public string Header { get; }

    [RelayCommand]
    private void PreviousMonth()
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        Month = _builder.Build(_displayedMonth, _today);
    }

    [RelayCommand]
    private void NextMonth()
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        Month = _builder.Build(_displayedMonth, _today);
    }
}
