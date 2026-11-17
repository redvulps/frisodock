using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

internal interface IKeyboardHookPlatform
{
    uint CreateMessageQueue();
    nint Install(NativeTypes.LowLevelKeyboardProc callback);
    int GetMessage(out MSG message);
    void Dispatch(ref MSG message);
    void PostQuit(uint threadId);
    void Uninstall(nint hook);
}

internal sealed class KeyboardHookPlatform : IKeyboardHookPlatform
{
    public uint CreateMessageQueue()
    {
        // PostThreadMessage fails until the target thread has created its message queue.
        NativeMethods.PeekMessage(out _, 0, 0, 0, NativeConstants.PM_NOREMOVE);
        return NativeMethods.GetCurrentThreadId();
    }

    public nint Install(NativeTypes.LowLevelKeyboardProc callback) => NativeMethods.SetWindowsHookEx(
        NativeConstants.WH_KEYBOARD_LL, callback, NativeMethods.GetModuleHandle(null), 0);

    public int GetMessage(out MSG message) => NativeMethods.GetMessage(out message, 0, 0, 0);

    public void Dispatch(ref MSG message)
    {
        NativeMethods.TranslateMessage(ref message);
        NativeMethods.DispatchMessage(ref message);
    }

    public void PostQuit(uint threadId)
    {
        NativeMethods.PostThreadMessage(threadId, NativeConstants.WM_QUIT, 0, 0);
    }

    public void Uninstall(nint hook)
    {
        NativeMethods.UnhookWindowsHookEx(hook);
    }
}
