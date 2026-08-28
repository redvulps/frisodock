using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.App.Services;
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
    private readonly DockSettingsService _settings;
    private readonly DispatcherTimer _timer;

    private bool _started;
    private bool _disposed;

    [ObservableProperty]
    private string _time = string.Empty;

    [ObservableProperty]
    private string _date = string.Empty;

    [ObservableProperty]
    private string _tooltip = string.Empty;

    public ClockViewModel(IClock clock, ClockFormatter formatter, DockSettingsService settings)
    {
        _clock = clock;
        _formatter = formatter;
        _settings = settings;

        _timer = new DispatcherTimer(DispatcherPriority.Background);
        _timer.Tick += OnTick;

        _settings.Changed += OnSettingsChanged;

        Update();
    }

    /// <summary>Starts updating. Separate from the constructor so the clock does not run in tests.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _started = true;
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
        _settings.Changed -= OnSettingsChanged;
        _disposed = true;
    }

    /// <summary>
    /// Turning seconds on changes the text and the cadence: without rescheduling, the clock would spend
    /// up to a minute with the seconds frozen.
    /// </summary>
    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        // Metrics change when the regional format changes (RegionalFormatService re-measures
        // the clock): the text has to be re-rendered in the new culture, not only resized.
        if (!e.ClockChanged && !e.MetricsChanged)
        {
            return;
        }

        Update();

        if (_started)
        {
            ScheduleNextTick();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Update();
        ScheduleNextTick();
    }

    /// <summary>
    /// It reschedules on every tick instead of using a fixed interval: this way the update follows the
    /// rollover and does not accumulate drift.
    /// </summary>
    private void ScheduleNextTick()
    {
        _timer.Stop();
        _timer.Interval = _formatter.TimeUntilNextTick(_clock.Now, _settings.Current.ShowClockSeconds);
        _timer.Start();
    }

    private void Update()
    {
        DateTimeOffset now = _clock.Now;

        Time = _formatter.FormatTime(now, _settings.Current.ShowClockSeconds);
        Date = _formatter.FormatDate(now);
        Tooltip = _formatter.FormatTooltip(now);
    }
}
