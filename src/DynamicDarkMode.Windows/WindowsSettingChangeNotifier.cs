using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DynamicDarkMode.Windows;

internal sealed class WindowsSettingChangeNotifier : IWindowsSettingChangeNotifier
{
    private static readonly IntPtr BroadcastWindow = new(unchecked((int)0xFFFF));
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoBlock = 0x0001;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint BroadcastTimeoutMilliseconds = 5000;
    private const string ImmersiveColorSet = "ImmersiveColorSet";

    public void BroadcastThemeChanged()
    {
        IntPtr result = SendMessageTimeout(
            hWnd: BroadcastWindow,
            message: WmSettingChange,
            wParam: IntPtr.Zero,
            lParam: ImmersiveColorSet,
            flags: SmtoBlock | SmtoAbortIfHung,
            timeout: BroadcastTimeoutMilliseconds,
            result: out _);

        if (result == IntPtr.Zero)
        {
            throw new Win32Exception(
                error: Marshal.GetLastWin32Error(),
                message: "Windows did not accept the theme-change notification.");
        }
    }

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        EntryPoint = "SendMessageTimeoutW",
        SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
}
