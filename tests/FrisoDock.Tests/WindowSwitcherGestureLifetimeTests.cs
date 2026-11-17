using FrisoDock.Interop.Native;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class WindowSwitcherGestureLifetimeTests
{
    [Fact]
    public async Task LateStartup_AfterStop_DoesNotInstallOrSignalADisposedWaitHandle()
    {
        using var platform = new Platform();
        platform.StartGate.Reset();
        using var gesture = new WindowSwitcherGesture(platform, TimeSpan.FromMilliseconds(20));
        gesture.SetModes(true, false);
        Task completion = gesture.Completion;
        try
        {
            await platform.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            gesture.Stop();
        }
        finally
        {
            platform.StartGate.Set();
        }

        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, platform.InstallCount);
    }

    [Fact]
    public async Task StopTimeout_KeepsTheOldHookAliveUntilCleanup_BeforeRestarting()
    {
        using var platform = new Platform { DelayQuit = true };
        using var gesture = new WindowSwitcherGesture(platform, TimeSpan.FromMilliseconds(20));
        gesture.SetModes(true, false);
        await platform.Installed.WaitAsync(TimeSpan.FromSeconds(5));
        await platform.Pumping.WaitAsync(TimeSpan.FromSeconds(5));
        Task completion = gesture.Completion;
        try
        {
            gesture.Stop();
            gesture.SetModes(true, false);
            Assert.Equal(1, platform.InstallCount);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Assert.True(platform.Callback!.TryGetTarget(out _));
        }
        finally
        {
            platform.DelayQuit = false;
            platform.ExitGate.Set();
        }

        await completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await platform.Installed.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, platform.MaxActive);
        Task replacement = gesture.Completion;
        gesture.Stop();
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, platform.Active);
    }

    private sealed class Platform : IKeyboardHookPlatform, IDisposable
    {
        public readonly ManualResetEventSlim StartGate = new(true);
        public readonly ManualResetEventSlim ExitGate = new();
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly SemaphoreSlim Installed = new(0);
        public readonly SemaphoreSlim Pumping = new(0);
        public volatile bool DelayQuit;
        public int InstallCount;
        public int Active;
        public int MaxActive;
        public WeakReference<NativeTypes.LowLevelKeyboardProc>? Callback;

        public uint CreateMessageQueue()
        {
            Started.TrySetResult();
            if (!StartGate.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test did not release startup");
            }

            return 1;
        }

        public nint Install(NativeTypes.LowLevelKeyboardProc callback)
        {
            ExitGate.Reset();
            Callback = new WeakReference<NativeTypes.LowLevelKeyboardProc>(callback);
            Interlocked.Increment(ref InstallCount);
            int active = Interlocked.Increment(ref Active);
            MaxActive = Math.Max(MaxActive, active);
            Installed.Release();
            return 1;
        }

        public int GetMessage(out MSG message)
        {
            Pumping.Release();
            if (!ExitGate.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test did not release the message pump");
            }

            message = default;
            return 0;
        }

        public void Dispatch(ref MSG message) { }
        public void PostQuit(uint threadId)
        {
            if (!DelayQuit)
            {
                ExitGate.Set();
            }
        }

        public void Uninstall(nint hook) => Interlocked.Decrement(ref Active);
        public void Dispose()
        {
            StartGate.Dispose();
            ExitGate.Dispose();
            Installed.Dispose();
            Pumping.Dispose();
        }
    }
}
