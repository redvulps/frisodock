using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Opens/closes the Start menu. That is all (SRP).
///
/// Simulating the Windows key is deliberately the simplest and most stable approach: the menu is
/// hosted by StartMenuExperienceHost, a process separate from the taskbar, so it keeps
/// working with the taskbar hidden — and it anchors itself to the bottom edge, appearing above
/// the dock. The alternatives (undocumented shell COM) break with every Windows version.
/// </summary>
public sealed class StartMenuInvoker : IStartMenuInvoker
{
    public void Toggle()
    {
        int inputSize = Marshal.SizeOf<INPUT>();

        INPUT[] sequence =
        [
            CreateKeyInput(NativeConstants.VK_LWIN, isKeyUp: false),
            CreateKeyInput(NativeConstants.VK_LWIN, isKeyUp: true),
        ];

        NativeMethods.SendInput((uint)sequence.Length, sequence, inputSize);
    }

    private static INPUT CreateKeyInput(ushort virtualKey, bool isKeyUp)
    {
        return new INPUT
        {
            type = NativeConstants.INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = virtualKey,
                    dwFlags = isKeyUp ? NativeConstants.KEYEVENTF_KEYUP : 0,
                },
            },
        };
    }
}
