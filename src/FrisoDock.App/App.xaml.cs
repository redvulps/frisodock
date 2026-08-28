using System.Windows;
using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FrisoDock.App;

/// <summary>
/// Composition root and owner of the application lifetime.
///
/// The non-negotiable rule of this class: the native taskbar MUST come back. We are the ones
/// hiding it, so every exit path — normal shutdown, unhandled exception, logoff or
/// system shutdown — has to restore it. Leaving the user with no taskbar and no dock is the
/// worst possible outcome, and that is why the restore shows up in every handler below.
/// </summary>
public partial class App : Application
{
    private const string SingleInstanceMutexName = "Local\\FrisoDock.SingleInstance";

    private ServiceProvider? _services;
    private ITaskbarController? _taskbarController;
    private DockHost? _dockHost;
    private WindowSwitcherRunner? _windowSwitcher;
    private TrayHostRunner? _trayHost;
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CommandLineOptions options = CommandLineOptions.Parse(e.Args);

        if (options.RestoreTaskbarAndExit)
        {
            // Rescue mode: there is no dock and no container, just the standalone controller.
            new TaskbarController(new FileTaskbarStateStore(), new NoShellTrayPriority()).Restore();
            Shutdown();
            return;
        }

        if (!TryAcquireSingleInstanceLock())
        {
            // Two instances hiding and showing the taskbar would fight each other.
            MessageBox.Show(
                "O FrisoDock já está em execução.",
                "FrisoDock",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        RegisterFailSafeHandlers();

        // Configuration comes from disk; the command line flags are overrides for this session.
        var settingsStore = new JsonDockSettingsStore();
        DockSettings settings = options.ApplyTo(settingsStore.Load());

        // The clock widths depend on the culture and the system font, which are only known
        // here. Without this measurement, the dock reserves space by a constant that fits one
        // machine only — and on a vertical dock that constant becomes panel thickness.
        settings = settings with
        {
            Metrics = ClockWidthMeasurer.Measure(settings.Metrics, new ClockFormatter()),
        };

        _services = BuildServiceProvider(settingsStore, settings);
        _taskbarController = _services.GetRequiredService<ITaskbarController>();

        if (settings.HideNativeTaskbar)
        {
            _taskbarController.Hide(settings.Edge);
        }
        else
        {
            // Hiding is now ShowWindow, and ShowWindow does not undo itself: if the previous
            // run died while hiding, the taskbar would stay gone. Showing it on the way in
            // fixes that at no cost — on an already visible taskbar it does nothing.
            _taskbarController.Restore();
        }

        // Before any window: the accent brushes have to be in the application dictionary
        // by the time the first XAML is loaded, otherwise the first paint comes out with the
        // App.xaml seed and would only correct itself on the next color change.
        _services.GetRequiredService<AccentTheme>().Start();

        _trayHost = _services.GetRequiredService<TrayHostRunner>();
        _trayHost.Start();

        _dockHost = _services.GetRequiredService<DockHost>();
        _dockHost.Start();

        _windowSwitcher = _services.GetRequiredService<WindowSwitcherRunner>();
        _windowSwitcher.Start();

        MainWindow = _dockHost.PrimaryWindow;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The tray goes back to Explorer before anything else: the docks disappear right after,
        // and an icon left hanging on an already closed window would never come back.
        _trayHost?.Dispose();

        // The keyboard hook goes before the windows: while it exists, every Alt+Tab in the
        // system passes through here, and an already torn down dock would have nothing to show.
        _windowSwitcher?.Dispose();
        _dockHost?.Dispose();

        RestoreTaskbar();

        _services?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>
    /// Registers the restore safety nets. Each one covers a different way for the
    /// process to end without going through <see cref="OnExit"/>.
    /// </summary>
    private void RegisterFailSafeHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        SessionEnding += OnSessionEnding;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RestoreTaskbar();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        RestoreTaskbar();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        RestoreTaskbar();
    }

    private void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
    {
        RestoreTaskbar();
    }

    /// <summary>Idempotent by construction: it can be called by several handlers in a row.</summary>
    private void RestoreTaskbar()
    {
        _taskbarController?.Restore();
    }

    private bool TryAcquireSingleInstanceLock()
    {
        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);

        if (createdNew)
        {
            return true;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
        return false;
    }

    /// <summary>
    /// Composition root: the only place in the app that knows the concrete implementations.
    /// Everything else depends only on the Core abstractions.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(IDockSettingsStore store, DockSettings settings)
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(store);
        services.AddSingleton(new DockSettingsService(store, settings));

        // Domain (pure, testable)
        services.AddSingleton<DockItemAggregator>();
        services.AddSingleton<WindowMonitorMatcher>();
        services.AddSingleton<PinnedAppsReorder>();
        services.AddSingleton<ClockFormatter>();
        services.AddSingleton<MagnificationCurve>();
        services.AddSingleton<DockVisibilityPolicy>();
        services.AddSingleton<WindowSwitchList>();
        services.AddSingleton<DockLayoutCalculator>();

        // Win32 (Interop layer)
        services.AddSingleton<ITaskbarStateStore, FileTaskbarStateStore>();
        services.AddSingleton<ITaskbarController, TaskbarController>();
        services.AddSingleton<IWindowEnumerator, WindowEnumerator>();
        services.AddSingleton<IWindowActivator, WindowActivator>();
        services.AddSingleton<IStartMenuInvoker, StartMenuInvoker>();
        services.AddSingleton<IIconExtractor, IconExtractor>();
        services.AddSingleton<IAppLauncher, AppLauncher>();
        services.AddSingleton<IScreenProvider, ScreenProvider>();
        services.AddSingleton<ICursorProvider, CursorProvider>();
        services.AddSingleton<IDisplayWatcher, DisplayWatcher>();
        services.AddSingleton<IWindowPositioner, WindowPositioner>();
        services.AddSingleton<IWindowSwitcherExclusion, WindowSwitcherExclusion>();
        services.AddSingleton<IWindowActivationPolicy, WindowActivationPolicy>();
        services.AddSingleton<IWindowSwitcherGesture, WindowSwitcherGesture>();
        services.AddSingleton<IShellRestartWatcher, ShellRestartWatcher>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IWindowBackdrop, DwmWindowBackdrop>();
        services.AddSingleton<ShellLinkReader>();
        services.AddSingleton<CustomDestinationsParser>();
        services.AddSingleton<AutomaticDestinationsParser>();
        services.AddSingleton<IJumpListProvider, JumpListProvider>();
        services.AddSingleton<ITrayHost, TrayHost>();

        // Same instance in both roles: the one that yields is the tray host, which is what took it.
        services.AddSingleton<IShellTrayPriority>(provider => (IShellTrayPriority)provider.GetRequiredService<ITrayHost>());
        services.AddSingleton<IWindowThumbnailService, DwmThumbnailService>();
        services.AddSingleton<IRadioController, RadioController>();
        services.AddSingleton<IBrightnessController, BrightnessController>();
        services.AddSingleton<IVolumeController, VolumeController>();
        services.AddSingleton<IBatteryProvider, BatteryProvider>();
        services.AddSingleton<INetworkProvider, NetworkProvider>();
        services.AddSingleton<ISettingsPageLauncher, SettingsPageLauncher>();
        services.AddSingleton<IAccentColorProvider, SystemAccentColorProvider>();

        // Host infrastructure
        services.AddSingleton<IApplicationLifetime, WpfApplicationLifetime>();
        services.AddSingleton<IPinnedAppStore, JsonPinnedAppStore>();
        services.AddSingleton<PinnedAppsService>();
        services.AddSingleton<IconImageProvider>();
        services.AddSingleton<TrayHostRunner>();
        services.AddSingleton<WindowSwitcherRunner>();
        services.AddSingleton<DockHost>();
        services.AddSingleton<JumpListFlyoutFactory>();
        services.AddSingleton<JumpListWarmer>();
        services.AddSingleton<TrayFlyoutFactory>();
        services.AddSingleton<QuickSettingsFlyoutFactory>();
        services.AddSingleton<SettingsWindowFactory>();
        services.AddSingleton<AccentTheme>();

        // The pinned list is a single one, shared by every dock.
        services.AddSingleton<IPinnedAppsEditor>(provider => provider.GetRequiredService<PinnedAppsService>());

        // Presentation
        services.AddSingleton<ClockViewModel>();
        services.AddSingleton<QuickStatusViewModel>();

        // One dock per monitor: each is born in a scope, with its own appbar, its own
        // placement and its own item list. Sharing any of the three would make the
        // docks fight over the same window handle.
        services.AddScoped<DockMonitorHolder>();
        services.AddScoped<IAppBarService, AppBarService>();
        services.AddScoped<DockPlacementService>();
        services.AddScoped<DockViewModel>();
        services.AddScoped<DockWindow>();

        return services.BuildServiceProvider();
    }
}
