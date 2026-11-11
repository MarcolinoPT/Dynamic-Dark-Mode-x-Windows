using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DynamicDarkMode.ThemeActivationSpike;

// Experimental, undocumented Windows COM interfaces.
// The vtable declarations are based on the MIT-licensed reference implementation
// credited in THIRD-PARTY-NOTICES.md. They are not a Microsoft-supported contract.

[Flags]
internal enum ThemeManagerInitializationFlags
{
    None = 0,
    CurrentThemeOnly = 1 << 0,
    Unknown1 = 1 << 1,
    Unknown2 = 1 << 2
}

[Flags]
internal enum ThemeApplyFlags
{
    None = 0,
    IgnoreBackground = 1 << 0,
    IgnoreCursor = 1 << 1,
    IgnoreDesktopIcons = 1 << 2,
    IgnoreColor = 1 << 3,
    IgnoreSound = 1 << 4,
    IgnoreScreensaver = 1 << 5,
    UnknownWindowMetrics = 1 << 6,
    Unknown2 = 1 << 7,
    NoHourglass = 1 << 8
}

[Flags]
internal enum ThemePackFlags
{
    None = 0,
    Unknown1 = 1 << 0,
    Unknown2 = 1 << 1,
    Silent = 1 << 2,
    Roamed = 1 << 3
}

[ComImport]
[Guid("26e4185f-0528-475f-acaf-abe89ba6017d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsTheme
{
    string DisplayName { get; set; }
    string VisualStyle1 { get; set; }
    string VisualStyle2 { get; set; }
}

[ComImport]
[Guid("c1e8c83e-845d-4d95-81db-e283fdffc000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IThemeManager2
{
    void Init(ThemeManagerInitializationFlags initFlags);
    void InitAsync(IntPtr hwnd, int unknown);
    void Refresh();
    void RefreshAsync(IntPtr hwnd, int unknown);
    void RefreshComplete();
    void GetThemeCount(out int count);
    void GetTheme(int index, out IWindowsTheme theme);
    void IsThemeDisabled(int index, out int disabled);
    void GetCurrentTheme(out int index);
    void SetCurrentTheme(
        IntPtr parentWindow,
        int themeIndex,
        int applyNow,
        ThemeApplyFlags applyFlags,
        ThemePackFlags packFlags);
    void GetCustomTheme(out int index);
    void GetDefaultTheme(out int index);
    void CreateThemePack(IntPtr parentWindow, string unknown, ThemePackFlags packFlags);
    void CloneAndSetCurrentTheme(IntPtr parentWindow, string unknown, out string result);
    void InstallThemePack(
        IntPtr parentWindow,
        string path,
        int unknown,
        ThemePackFlags packFlags,
        out string result,
        out IWindowsTheme theme);
    void DeleteTheme(string path);
    void OpenTheme(IntPtr parentWindow, string path, ThemePackFlags packFlags);
    void AddAndSelectTheme(
        IntPtr parentWindow,
        string path,
        ThemeApplyFlags applyFlags,
        ThemePackFlags packFlags);
    void SQMCurrentTheme();
    void ExportRoamingThemeToStream(IStream stream, int unknown);
    void ImportRoamingThemeFromStream(IStream stream, int unknown);
    void UpdateColorSettingsForLogonUI();
    void GetDefaultThemeId(out Guid id);
    void UpdateCustomTheme();
}

internal static class ThemeManagerNativeMethods
{
    internal const uint ClsctxAll = 0x17;
    internal const uint CoInitApartmentThreaded = 0x2;

    internal static readonly Guid ThemeManagerClassId =
        new("9324da94-50ec-4a14-a770-e90ca03e7c8f");

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern void CoUninitialize();

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int CoCreateInstance(
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid classId,
        IntPtr outer,
        uint classContext,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IThemeManager2? instance);
}
