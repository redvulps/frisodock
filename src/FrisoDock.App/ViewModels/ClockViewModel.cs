using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Services;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// The dock clock. Keeps time, date and tooltip up to date. That is all (SRP): formatting belongs
/// to <see cref="ClockFormatter"/> and telling the time to <see cref="IClock"/>.
/// </summary>
public sealed partial class ClockViewModel : ObservableObject, IDisposable
{
    private readonly IClock _clock;
    private readonly ClockFormatter _formatter;
    private readonly DispatcherTimer _timer;

    private bool _disposed;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _tooltip = string.Empty;

    public ClockViewModel(IClock clock, ClockFormatter formatter)
    {
        _clock = clock;
        _formatter = formatter;

        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += OnTick;

        Update();
    }

    /// <summary>Starts updating. Separate from the constructor so the clock does not run in tests.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ScheduleNextTick();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Update();
        ScheduleNextTick();
    }

    /// <summary>
    /// It reschedules on every tick instead of using a fixed interval: this way the update follows the
    /// minute rollover and does not accumulate drift.
    /// </summary>
    private void ScheduleNextTick()
    {
        _timer.Stop();
        _timer.Interval = _formatter.TimeUntilNextMinute(_clock.Now);
        _timer.Start();
    }

    private void Update()
    {
        DateTimeOffset now = _clock.Now;

        Time = _formatter.FormatTime(now);
        Date = _formatter.FormatDate(now);
        Tooltip = _formatter.FormatTooltip(now);
    }
}
