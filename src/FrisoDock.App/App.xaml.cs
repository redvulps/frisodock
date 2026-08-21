using System.Windows;
using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
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
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CommandLineOptions options = CommandLineOptions.Parse(e.Args);

        if (options.RestoreTaskbarAndExit)
        {
            // Rescue mode: there is no dock and no container, just the standalone controller.
            new TaskbarController(new FileTaskbarStateStore()).Restore();
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

        DockSettings settings = options.ApplyTo(new DockSettings());

        _services = BuildServiceProvider(settings);
        _taskbarController = _services.GetRequiredService<ITaskbarController>();

        if (settings.HideNativeTaskbar)
        {
            _taskbarController.Hide();
        }

        DockWindow window = _services.GetRequiredService<DockWindow>();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
    private static ServiceProvider BuildServiceProvider(DockSettings settings)
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(settings);

        // Domain (pure, testable)
        services.AddSingleton<DockItemAggregator>();
        services.AddSingleton<ClockFormatter>();
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
        services.AddSingleton<IWindowPositioner, WindowPositioner>();
        services.AddSingleton<IAppBarService, AppBarService>();
        services.AddSingleton<IShellRestartWatcher, ShellRestartWatcher>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IWindowBackdrop, DwmWindowBackdrop>();
        services.AddSingleton<CustomDestinationsParser>();
        services.AddSingleton<IJumpListProvider, JumpListProvider>();
        services.AddSingleton<ITrayHost, TrayHost>();

        // Host infrastructure
        services.AddSingleton<IApplicationLifetime, WpfApplicationLifetime>();
        services.AddSingleton<IPinnedAppStore, JsonPinnedAppStore>();
        services.AddSingleton<IconImageProvider>();
        services.AddSingleton<DockPlacementService>();
        services.AddSingleton<JumpListFlyoutFactory>();
        services.AddSingleton<TrayFlyoutFactory>();

        // Presentation
        services.AddSingleton<ClockViewModel>();
        services.AddSingleton<DockViewModel>();

        // The bar is what edits the pinned list: it exposes the same instance through both doors.
        services.AddSingleton<IPinnedAppsEditor>(provider => provider.GetRequiredService<DockViewModel>());
        services.AddSingleton<DockWindow>();

        return services.BuildServiceProvider();
    }
}
