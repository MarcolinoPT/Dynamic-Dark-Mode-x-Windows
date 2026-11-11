using System.Runtime.InteropServices;

namespace DynamicDarkMode.Windows.ThemeCatalog;

internal sealed record ThemeManagerEntry(
    int EnumerationIndex,
    string DisplayName,
    string VisualStylePath,
    string ColorStyle,
    bool IsDisabled);

internal interface IThemeManagerEntrySource
{
    IReadOnlyList<ThemeManagerEntry> GetEntries();
}

internal sealed class WindowsThemeManagerEntrySource : IThemeManagerEntrySource
{
    public IReadOnlyList<ThemeManagerEntry> GetEntries()
    {
        IReadOnlyList<ThemeManagerEntry>? entries = null;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                entries = GetEntriesOnStaThread();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.Name = "Dynamic Dark Mode theme catalog COM";
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException(
                "Windows theme entries could not be enumerated.",
                failure);
        }

        return entries ?? throw new InvalidOperationException(
            "Windows theme enumeration completed without returning entries.");
    }

    private static IReadOnlyList<ThemeManagerEntry> GetEntriesOnStaThread()
    {
        int initializationResult = ThemeManagerNativeMethods.CoInitializeEx(
            reserved: IntPtr.Zero,
            coInit: ThemeManagerNativeMethods.CoInitApartmentThreaded);

        if (initializationResult < 0)
        {
            Marshal.ThrowExceptionForHR(initializationResult);
        }

        IThemeManager2? manager = null;
        try
        {
            int creationResult = ThemeManagerNativeMethods.CoCreateInstance(
                classId: ThemeManagerNativeMethods.ThemeManagerClassId,
                outer: IntPtr.Zero,
                classContext: ThemeManagerNativeMethods.ClsctxAll,
                interfaceId: typeof(IThemeManager2).GUID,
                instance: out manager);

            if (creationResult < 0)
            {
                Marshal.ThrowExceptionForHR(creationResult);
            }

            if (manager is null)
            {
                throw new InvalidOperationException(
                    "Windows returned no IThemeManager2 instance.");
            }

            manager.Init(ThemeManagerInitializationFlags.None);
            manager.GetThemeCount(out int count);
            if (count < 0 || count > 4096)
            {
                throw new InvalidDataException(
                    $"Windows returned an implausible theme entry count: {count}.");
            }

            var entries = new List<ThemeManagerEntry>(capacity: count);
            for (int index = 0; index < count; index++)
            {
                manager.GetTheme(index, out IWindowsThemeEntry theme);
                if (theme is null)
                {
                    throw new InvalidDataException(
                        $"Windows returned no theme metadata for entry {index}.");
                }

                try
                {
                    manager.IsThemeDisabled(index, out int disabled);
                    entries.Add(new ThemeManagerEntry(
                        EnumerationIndex: index,
                        DisplayName: theme.DisplayName ?? string.Empty,
                        VisualStylePath: theme.VisualStyle1 ?? string.Empty,
                        ColorStyle: theme.VisualStyle2 ?? string.Empty,
                        IsDisabled: disabled != 0));
                }
                finally
                {
                    ReleaseComObject(theme);
                }
            }

            return entries;
        }
        finally
        {
            ReleaseComObject(manager);
            ThemeManagerNativeMethods.CoUninitialize();
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }
}

[Flags]
internal enum ThemeManagerInitializationFlags
{
    None = 0,
    CurrentThemeOnly = 1 << 0,
    Unknown1 = 1 << 1,
    Unknown2 = 1 << 2
}

[ComImport]
[Guid("26e4185f-0528-475f-acaf-abe89ba6017d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsThemeEntry
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
    void GetTheme(int index, out IWindowsThemeEntry theme);
    void IsThemeDisabled(int index, out int disabled);
    void GetCurrentTheme(out int index);
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
