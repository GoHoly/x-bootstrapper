using System.Runtime.InteropServices;
using Caelus.Core;

namespace Caelus.Services;

public static class MultiInstanceService
{
    private static IntPtr _event = IntPtr.Zero;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateEvent(IntPtr attributes, bool manualReset, bool initialState, string name);

    public static void Enable()
    {
        if (_event != IntPtr.Zero)
            return;

        _event = CreateEvent(IntPtr.Zero, false, false, "ROBLOX_singletonEvent");
        Logger.Write("MultiInstance", "Opened ROBLOX_singletonEvent so multiple clients can run.");
    }
}
