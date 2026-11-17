using System.Windows.Interop;
using System.Windows.Threading;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class TrayForwardingTests
{
    private const int TestMessage = 0x0400 + 123;

    [Fact]
    public Task UnresponsiveReceiver_TimesOutWithoutWaitingForTheWindowProcedure() => DispatcherTest.RunAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var source = new HwndSource(new HwndSourceParameters("FrisoDock timeout test")
        {
            Width = 1,
            Height = 1,
            WindowStyle = 0,
        });
        source.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
        {
            if (message == TestMessage)
            {
                entered.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(5));
                handled = true;
                return 42;
            }

            return 0;
        });
        nint handle = source.Handle;
        // The receiver owns this dispatcher. Observe the timeout on a different thread,
        // since its window procedure deliberately cannot service dispatcher continuations.
        Task sender = Task.Run(async () =>
        {
            Task<nint> send = Task.Run(() => TrayHost.SendToExplorer(handle, TestMessage, 0, 0));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.Equal(0, await send.WaitAsync(TimeSpan.FromSeconds(3)));
            }
            finally
            {
                release.Set();
            }
        });
        await sender;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    });

    [Fact]
    public Task ResponsiveReceiver_ReturnsItsActualResult() => DispatcherTest.RunAsync(async () =>
    {
        using var source = new HwndSource(new HwndSourceParameters("FrisoDock forwarding test")
        {
            Width = 1,
            Height = 1,
            WindowStyle = 0,
        });
        source.AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
        {
            if (message == TestMessage)
            {
                handled = true;
                return 42;
            }

            return 0;
        });
        nint handle = source.Handle;
        nint result = await Task.Run(() => TrayHost.SendToExplorer(handle, TestMessage, 0, 0));
        Assert.Equal(42, result);
    });
}
