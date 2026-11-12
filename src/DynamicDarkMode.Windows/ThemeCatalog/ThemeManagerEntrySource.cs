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
    /// <summary>Returns the entries exposed by the current Windows theme manager.</summary>
    IReadOnlyList<ThemeManagerEntry> GetEntries();
}

internal sealed class WindowsThemeManagerEntrySource : IThemeManagerEntrySource
{
    /// <summary>Enumerates theme-manager entries on a dedicated single-threaded apartment.</summary>
    /// <returns>The current theme-manager entries with their transient enumeration indices.</returns>
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
            return ReadEntries(manager);
        }
        finally
        {
            ReleaseComObject(manager);
            ThemeManagerNativeMethods.CoUninitialize();
        }
    }

    internal static IReadOnlyList<ThemeManagerEntry> ReadEntries(IThemeManager2 manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

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
internal interface IWindowsThemeEntry
{
    /// <summary>Gets or sets the theme's display name.</summary>
    string DisplayName { get; set; }
    /// <summary>Gets or sets the first VisualStyles path exposed by the theme manager.</summary>
    string VisualStyle1 { get; set; }
    /// <summary>Gets or sets the VisualStyles color-style identifier exposed by the theme manager.</summary>
    string VisualStyle2 { get; set; }
}

[ComImport]
[Guid("c1e8c83e-845d-4d95-81db-e283fdffc000")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IThemeManager2
{
    /// <summary>Initializes the theme manager with the requested enumeration flags.</summary>
    void Init(ThemeManagerInitializationFlags initFlags);
    /// <summary>Starts asynchronous theme-manager initialization.</summary>
    void InitAsync(IntPtr hwnd, int unknown);
    /// <summary>Refreshes the theme manager's current theme list.</summary>
    void Refresh();
    /// <summary>Starts an asynchronous refresh operation.</summary>
    void RefreshAsync(IntPtr hwnd, int unknown);
    /// <summary>Signals completion of an asynchronous refresh operation.</summary>
    void RefreshComplete();
    /// <summary>Returns the number of entries currently exposed by the theme manager.</summary>
    void GetThemeCount(out int count);
    /// <summary>Retrieves the theme metadata at a transient enumeration position.</summary>
    void GetTheme(int index, out IWindowsThemeEntry theme);
    /// <summary>Reports whether the entry at the specified position is disabled.</summary>
    void IsThemeDisabled(int index, out int disabled);
    /// <summary>Returns the current theme's transient enumeration position.</summary>
    void GetCurrentTheme(out int index);
    /// <summary>Requests activation of the entry at the specified transient position.</summary>
    void SetCurrentTheme(
        IntPtr parentWindow,
        int themeIndex,
        int applyNow,
        ThemeApplyFlags applyFlags,
        ThemePackFlags packFlags);
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
