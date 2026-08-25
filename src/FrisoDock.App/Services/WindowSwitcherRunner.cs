using System.Windows.Media;
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

        ApplyModes();
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
        if (e.WindowSwitcherChanged || e.SameAppWindowSwitcherChanged)
        {
            ApplyModes();
        }
    }

    /// <summary>
    /// Turns each gesture on as configured. With both off the hook goes away, and the keys
    /// become the Windows ones right away, with no dock restart.
    /// </summary>
    private void ApplyModes()
    {
        DockSettings settings = _settings.Current;

        _gesture.SetModes(settings.UseGroupedWindowSwitcher, settings.UseSameAppWindowSwitcher);

        if (!settings.UseGroupedWindowSwitcher && !settings.UseSameAppWindowSwitcher)
        {
            CloseView();
        }
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
            Begin(step.Scope, step.Backwards);
            return;
        }

        _viewModel.Select(_list.Step(_viewModel.Entries.Count, _viewModel.SelectedIndex, step.Backwards));
    }

    private void Begin(WindowSwitchScope scope, bool backwards)
    {
        IReadOnlyList<WindowInfo> windows = _windows.GetWindows();
        nint foreground = FindForeground(windows);

        IReadOnlyList<WindowSwitcherEntryViewModel> entries = scope == WindowSwitchScope.SameApp
            ? BuildSameAppEntries(windows, foreground)
            : BuildAppEntries(windows);

        // Nothing to switch to: with a single app (or a single window, in the same-app gesture) the
        // switcher would have nowhere to go, and opening it would only flash a window on screen.
        if (entries.Count < 2)
        {
            return;
        }

        _previousForeground = foreground;

        var viewModel = new WindowSwitcherViewModel(entries);
        viewModel.Select(_list.SelectFirst(entries.Count, backwards));

        var view = new WindowSwitcherWindow(
            viewModel,
            ResolveMonitor(),
            _backdrop,
            _positioner,
            _switcherExclusion);

        _viewModel = viewModel;
        _view = view;

        view.Show();

        // The focus comes from the activator, and not from Window.Activate: Windows refuses a focus
        // change to whoever is not in the foreground, which is exactly our case. With the focus on us,
        // the Alt release lands on a window with no menu bar. Left on the app that was in front,
        // it would arrive there without the Tab that came with it, and many apps read that as the
        // shortcut that opens the menu.
        _activator.Activate(view.Handle);

        // The focus safety net is armed only now, and not before the Show.
        //
        // Showing a window from a process that is not the foreground — the case here,
        // because what is in front is the user's app — causes a focus round trip
        // before the activator pins the focus on us. A Deactivated armed before that
        // would close the window during that bounce, before it appeared. Deferred to the end of the
        // queue, it only takes effect once the focus has settled, and then it serves its purpose:
        // closing when the user clicks outside.
        _dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_view == view)
            {
                view.Deactivated += OnViewDeactivated;
            }
        });
    }

    /// <summary>One item per running app, for Alt+Tab.</summary>
    private IReadOnlyList<WindowSwitcherEntryViewModel> BuildAppEntries(IReadOnlyList<WindowInfo> windows)
    {
        return _list.Build(windows)
            .Select(app => WindowSwitcherEntryViewModel.ForApp(app, _icons.GetIcon(app)))
            .ToList();
    }

    /// <summary>
    /// One item per window of the focused app, for Alt+'.
    ///
    /// The icon is resolved once and reused in every entry: the windows belong to the same app,
    /// so the icon is the same, and extracting one per window would be repeated work. What tells
    /// the items apart is the title, which goes into the label.
    /// </summary>
    private IReadOnlyList<WindowSwitcherEntryViewModel> BuildSameAppEntries(
        IReadOnlyList<WindowInfo> windows,
        nint foreground)
    {
        IReadOnlyList<WindowInfo> appWindows = _list.BuildSameApp(windows, foreground);

        if (appWindows.Count == 0)
        {
            return [];
        }

        WindowInfo first = appWindows[0];
        var appItem = new DockItem(first.Key, first.DisplayName, first.ExecutablePath, null, appWindows);
        ImageSource? image = _icons.GetIcon(appItem);

        return appWindows
            .Select(window => WindowSwitcherEntryViewModel.ForWindow(window, image))
            .ToList();
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
