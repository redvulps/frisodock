using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Wires the Alt+Tab gesture to the switcher window: builds the list, moves the selection and
/// activates the chosen app. That is all (SRP) — recognizing the keys belongs to
/// <see cref="IWindowSwitcherGesture"/>, grouping to <see cref="WindowSwitchList"/>, drawing to the window.
///
/// It lives outside the docks, and not inside one of them, because there is a single switcher:
/// with a dock per monitor, hosting it in one would make the screen it appears on depend on which
/// dock was born first.
/// </summary>
public sealed class WindowSwitcherRunner : IDisposable
{
    private readonly IWindowSwitcherGesture _gesture;
    private readonly IWindowEnumerator _windows;
    private readonly IWindowActivator _activator;
    private readonly WindowSwitchList _list;
    private readonly IconImageProvider _icons;
    private readonly DockSettingsService _settings;
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowPositioner _positioner;
    private readonly IWindowSwitcherExclusion _switcherExclusion;
    private readonly IScreenProvider _screens;
    private readonly ICursorProvider _cursor;
    private readonly Dispatcher _dispatcher;

    private WindowSwitcherWindow? _view;
    private WindowSwitcherViewModel? _viewModel;

    /// <summary>
    /// Who had the focus when the gesture began, so it can be given back if the user gives up.
    ///
    /// It has to be stored right at the start: the switcher window takes the focus next, and from
    /// then on there is no way left to ask Windows who had it before.
    /// </summary>
    private nint _previousForeground;

    private bool _disposed;

    public WindowSwitcherRunner(
        IWindowSwitcherGesture gesture,
        IWindowEnumerator windows,
        IWindowActivator activator,
        WindowSwitchList list,
        IconImageProvider icons,
        DockSettingsService settings,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IWindowSwitcherExclusion switcherExclusion,
        IScreenProvider screens,
        ICursorProvider cursor)
    {
        _gesture = gesture;
        _windows = windows;
        _activator = activator;
        _list = list;
        _icons = icons;
        _settings = settings;
        _backdrop = backdrop;
        _positioner = positioner;
        _switcherExclusion = switcherExclusion;
        _screens = screens;
        _cursor = cursor;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        _gesture.Stepped += OnStepped;
        _gesture.Committed += OnCommitted;
        _gesture.Cancelled += OnCancelled;
        _settings.Changed += OnSettingsChanged;

        ApplyMode();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gesture.Stepped -= OnStepped;
        _gesture.Committed -= OnCommitted;
        _gesture.Cancelled -= OnCancelled;
        _settings.Changed -= OnSettingsChanged;

        _gesture.Stop();
        CloseView();

        _disposed = true;
    }

    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        if (e.WindowSwitcherChanged)
        {
            ApplyMode();
        }
    }

    /// <summary>
    /// Turns the hook on or off as configured. Off, Alt+Tab goes back to being the Windows one
    /// right away, with no dock restart.
    /// </summary>
    private void ApplyMode()
    {
        if (_settings.Current.UseGroupedWindowSwitcher)
        {
            _gesture.Start();
            return;
        }

        _gesture.Stop();
        CloseView();
    }

    // All three events arrive from the hook thread, which is in the path of every keystroke.
    // The work is dispatched without waiting on purpose: blocking there would slow all typing down.

    private void OnStepped(object? sender, WindowSwitcherStepEventArgs e)
    {
        _dispatcher.BeginInvoke(() => Step(e));
    }

    private void OnCommitted(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(Commit);
    }

    private void OnCancelled(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(Cancel);
    }

    private void Step(WindowSwitcherStepEventArgs step)
    {
        if (_viewModel is null)
        {
            Begin(step.Backwards);
            return;
        }

        _viewModel.Select(_list.Step(_viewModel.Entries.Count, _viewModel.SelectedIndex, step.Backwards));
    }

    private void Begin(bool backwards)
    {
        IReadOnlyList<WindowInfo> windows = _windows.GetWindows();
        IReadOnlyList<DockItem> apps = _list.Build(windows);

        if (apps.Count == 0)
        {
            return;
        }

        _previousForeground = FindForeground(windows);

        var viewModel = new WindowSwitcherViewModel(
            apps.Select(app => new WindowSwitcherEntryViewModel(app, _icons.GetIcon(app))));

        viewModel.Select(_list.SelectFirst(apps.Count, backwards));

        var view = new WindowSwitcherWindow(
            viewModel,
            ResolveMonitor(),
            _backdrop,
            _positioner,
            _switcherExclusion);

        // Losing focus ends the session: it is the safety net for the case where the Alt release
        // never reaches the hook — without it the window would stay on screen with nothing to close it.
        view.Deactivated += OnViewDeactivated;

        _viewModel = viewModel;
        _view = view;

        view.Show();

        // The focus comes from the activator, and not from Window.Activate: Windows refuses a focus
        // change to whoever is not in the foreground, which is exactly our case. With the focus on us,
        // the Alt release lands on a window with no menu bar. Left on the app that was in front,
        // it would arrive there without the Tab that came with it, and many apps read that as the
        // shortcut that opens the menu.
        _activator.Activate(view.Handle);
    }

    private void Commit()
    {
        WindowInfo? target = _viewModel?.Selected?.Target;

        // The window steps aside before the activation: closed afterwards, it would give the focus back
        // to whoever had it before, undoing the switch the user just asked for.
        CloseView();

        if (target is not null)
        {
            _activator.Activate(target);
        }
    }

    private void Cancel()
    {
        nint previous = _previousForeground;

        CloseView();

        if (previous != 0)
        {
            _activator.Activate(previous);
        }
    }

    private void OnViewDeactivated(object? sender, EventArgs e)
    {
        CloseView();
    }

    private void CloseView()
    {
        _viewModel = null;
        _previousForeground = 0;

        if (_view is not WindowSwitcherWindow view)
        {
            return;
        }

        _view = null;

        view.Deactivated -= OnViewDeactivated;
        view.Close();
    }

    private static nint FindForeground(IReadOnlyList<WindowInfo> windows)
    {
        foreach (WindowInfo window in windows)
        {
            if (window.IsForeground)
            {
                return window.Handle;
            }
        }

        return 0;
    }

    /// <summary>
    /// The monitor the switcher appears on: the one under the cursor, with the primary as fallback.
    ///
    /// It is the screen the user is looking at. The focused app's would be another reasonable choice,
    /// but it disappears exactly when the switcher is most useful — with everything minimized.
    /// </summary>
    private MonitorInfo ResolveMonitor()
    {
        PixelPoint position = _cursor.GetPosition();

        foreach (MonitorInfo monitor in _screens.GetMonitors())
        {
            if (monitor.Bounds.Contains(position))
            {
                return monitor;
            }
        }

        return _screens.GetPrimaryMonitor();
    }
}
