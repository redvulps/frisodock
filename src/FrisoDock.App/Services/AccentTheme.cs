using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Publishes the Windows accent color as application brushes. That is all (SRP).
///
/// The XAML reads these keys through <c>DynamicResource</c>, and that is what makes a change reach
/// windows that are already open: swapping the dictionary entry re-evaluates whoever depends on it.
/// With <c>StaticResource</c> the value would be read once, at XAML load, and changing the accent
/// color in Windows would not show up until the dock restarted.
///
/// The keys are seeded in App.xaml with the default blue. The seed exists so the XAML opens
/// in the designer and so there is never an instant with a null brush — the real value comes in at startup.
/// </summary>
public sealed class AccentTheme : IDisposable
{
    public const string AccentBrushKey = "AccentBrush";
    public const string AccentTextBrushKey = "AccentTextBrush";

    private readonly IAccentColorProvider _provider;
    private readonly ResourceDictionary _resources;
    private readonly Dispatcher _dispatcher;

    public AccentTheme(IAccentColorProvider provider)
    {
        _provider = provider;
        _resources = Application.Current.Resources;
        _dispatcher = Application.Current.Dispatcher;
    }

    public void Start()
    {
        Apply(_provider.Current);
        _provider.Changed += OnAccentChanged;
    }

    public void Dispose()
    {
        _provider.Changed -= OnAccentChanged;
    }

    private void OnAccentChanged(object? sender, EventArgs e)
    {
        // The Windows notification arrives on a COM thread; brushes and the resource dictionary belong
        // to the UI thread.
        _dispatcher.BeginInvoke(() => Apply(_provider.Current));
    }

    private void Apply(AccentPalette palette)
    {
        _resources[AccentBrushKey] = Frozen(palette.Accent);
        _resources[AccentTextBrushKey] = Frozen(palette.OnAccent);
    }

    /// <summary>
    /// Frozen because it is immutable by nature and shared by every window: a frozen brush
    /// skips the thread affinity check and spends no change notification.
    /// </summary>
    private static SolidColorBrush Frozen(AccentColor color)
    {
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
