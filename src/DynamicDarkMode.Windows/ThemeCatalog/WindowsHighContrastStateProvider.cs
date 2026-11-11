using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DynamicDarkMode.Windows.ThemeCatalog;

internal interface IHighContrastStateProvider
{
    bool IsEnabled();
}

internal sealed class WindowsHighContrastStateProvider : IHighContrastStateProvider
{
    private const uint SpiGetHighContrast = 0x0042;
    private const uint HighContrastOn = 0x00000001;

    public bool IsEnabled()
    {
        var highContrast = new HighContrast
        {
            Size = (uint)Marshal.SizeOf<HighContrast>()
        };

        if (!SystemParametersInfo(
            action: SpiGetHighContrast,
            parameter: highContrast.Size,
            value: ref highContrast,
            flags: 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return (highContrast.Flags & HighContrastOn) != 0;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HighContrast
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        ref HighContrast value,
        uint flags);
}
