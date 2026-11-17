using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Native;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class CalendarFlyoutPresentationTests
{
    [Fact]
    public Task NativeComposition_KeepsTheCalendarCloakedUntilItsFirstFrameIsReady() => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation { Native = new DwmWindowBackdrop() };
        var window = new CalendarFlyoutWindow(
            new CalendarFlyoutViewModel(DateTimeOffset.Now, new CalendarMonthBuilder()),
            new PixelRect(100, 900, 200, 950), DockEdge.Bottom,
            presentation, presentation, presentation, presentation) { ShowActivated = false };
        try
        {
            window.Show();
            await presentation.Revealed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, presentation.CloakQueryResult);
            Assert.NotEqual(0, presentation.CloakDuringFlush & 1);
            Assert.Equal(0, presentation.CloakAfterReveal & 1);
            Assert.Equal(42, CountDays(window));
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task FirstFrame_StaysCloakedUntilTheDayControlsAndCompositionAreReady() => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation();
        var model = new CalendarFlyoutViewModel(DateTimeOffset.Now, new CalendarMonthBuilder());
        var window = new CalendarFlyoutWindow(model, new PixelRect(100, 900, 200, 950), DockEdge.Bottom,
            presentation, presentation, presentation, presentation) { ShowActivated = false };
        try
        {
            window.Show();
            await presentation.Flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(presentation.Cloaked);
            Assert.Equal(["cloak", "appearance", "position", "flush"], presentation.Events);
            Assert.Equal(42, CountDays(window));

            presentation.Composed.SetResult();
            await presentation.Revealed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(presentation.Cloaked);
            Assert.Equal("reveal", presentation.Events[^1]);
        }
        finally
        {
            window.Close();
            presentation.Composed.TrySetResult();
        }
    });

    [Fact]
    public Task ClosedDuringComposition_DoesNotRevealTheDismissedWindow() => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation();
        var window = new CalendarFlyoutWindow(
            new CalendarFlyoutViewModel(DateTimeOffset.Now, new CalendarMonthBuilder()),
            new PixelRect(100, 900, 200, 950), DockEdge.Bottom,
            presentation, presentation, presentation, presentation) { ShowActivated = false };
        try
        {
            window.Show();
            await presentation.Flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            window.Close();
            presentation.Composed.SetResult();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Assert.DoesNotContain("reveal", presentation.Events);
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
            presentation.Composed.TrySetResult();
        }
    });

    private static int CountDays(DependencyObject root)
    {
        int count = root is TextBlock { DataContext: CalendarDay } ? 1 : 0;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            count += CountDays(VisualTreeHelper.GetChild(root, i));
        }

        return count;
    }

    private sealed class Presentation : IWindowBackdrop, IWindowPositioner, IWindowSwitcherExclusion, IScreenProvider
    {
        public readonly List<string> Events = [];
        public readonly TaskCompletionSource Flushing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Composed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Revealed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cloaked;
        public IWindowBackdrop? Native;
        public int CloakQueryResult;
        public int CloakDuringFlush;
        public int CloakAfterReveal;
        private nint _handle;

        public void Apply(nint windowHandle, WindowBackdropMaterial material, bool darkMode)
        {
            Events.Add("appearance");
            Native?.Apply(windowHandle, material, darkMode);
        }

        public void SetCloaked(nint windowHandle, bool cloaked)
        {
            _handle = windowHandle;
            Native?.SetCloaked(windowHandle, cloaked);
            Cloaked = cloaked;
            Events.Add(cloaked ? "cloak" : "reveal");
            if (!cloaked)
            {
                if (Native is not null)
                {
                    NativeMethods.DwmGetWindowAttribute(_handle, NativeConstants.DWMWA_CLOAKED, out CloakAfterReveal, sizeof(int));
                }

                Revealed.TrySetResult();
            }
        }

        public Task FlushAsync()
        {
            Events.Add("flush");
            Flushing.TrySetResult();
            if (Native is not null)
            {
                CloakQueryResult = NativeMethods.DwmGetWindowAttribute(_handle, NativeConstants.DWMWA_CLOAKED,
                    out CloakDuringFlush, sizeof(int));
                return Native.FlushAsync();
            }

            return Composed.Task;
        }

        public void SetBounds(nint windowHandle, PixelRect bounds, bool topMost) => Events.Add("position");
        public void BringToTop(nint windowHandle) { }
        public void Exclude(nint windowHandle) { }
        public MonitorInfo GetPrimaryMonitor() => new(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true, 1);
        public IReadOnlyList<MonitorInfo> GetMonitors() => [GetPrimaryMonitor()];
    }
}
