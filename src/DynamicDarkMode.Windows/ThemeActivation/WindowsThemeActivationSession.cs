using System.Runtime.InteropServices;
using DynamicDarkMode.Windows.ThemeCatalog;

namespace DynamicDarkMode.Windows.ThemeActivation;

internal interface IThemeActivationSessionFactory
{
    IThemeActivationSession Create();
}

internal interface IThemeActivationSession : IDisposable
{
    IReadOnlyList<ThemeManagerEntry> GetEntries();

    void SetCurrentTheme(int themeIndex);

    ThemeManagerEntry GetCurrentTheme();
}

internal sealed class WindowsThemeActivationSessionFactory : IThemeActivationSessionFactory
{
    public IThemeActivationSession Create()
    {
        int creationResult = ThemeManagerNativeMethods.CoCreateInstance(
            classId: ThemeManagerNativeMethods.ThemeManagerClassId,
            outer: IntPtr.Zero,
            classContext: ThemeManagerNativeMethods.ClsctxAll,
            interfaceId: typeof(IThemeManager2).GUID,
            instance: out IThemeManager2? manager);

        if (creationResult < 0)
        {
            Marshal.ThrowExceptionForHR(creationResult);
        }

        if (manager is null)
        {
            throw new InvalidOperationException(
                "Windows returned no IThemeManager2 instance.");
        }

        try
        {
            manager.Init(ThemeManagerInitializationFlags.None);
            return new WindowsThemeActivationSession(manager);
        }
        catch
        {
            ReleaseComObject(manager);
            throw;
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

internal sealed class WindowsThemeActivationSession : IThemeActivationSession
{
    private IThemeManager2? _manager;

    internal WindowsThemeActivationSession(IThemeManager2 manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public IReadOnlyList<ThemeManagerEntry> GetEntries() =>
        WindowsThemeManagerEntrySource.ReadEntries(Manager);

    public void SetCurrentTheme(int themeIndex)
    {
        Manager.SetCurrentTheme(
            parentWindow: IntPtr.Zero,
            themeIndex: themeIndex,
            applyNow: 1,
            applyFlags: ThemeApplyFlags.None,
            packFlags: ThemePackFlags.None);
    }

    public ThemeManagerEntry GetCurrentTheme()
    {
        Manager.GetCurrentTheme(out int index);
        Manager.GetTheme(index, out IWindowsThemeEntry theme);
        if (theme is null)
        {
            throw new InvalidDataException(
                "Windows returned no metadata for the current theme.");
        }

        try
        {
            Manager.IsThemeDisabled(index, out int disabled);
            return new ThemeManagerEntry(
                EnumerationIndex: index,
                DisplayName: theme.DisplayName ?? string.Empty,
                VisualStylePath: theme.VisualStyle1 ?? string.Empty,
                ColorStyle: theme.VisualStyle2 ?? string.Empty,
                IsDisabled: disabled != 0);
        }
        finally
        {
            if (Marshal.IsComObject(theme))
            {
                Marshal.ReleaseComObject(theme);
            }
        }
    }

    public void Dispose()
    {
        IThemeManager2? manager = _manager;
        _manager = null;

        if (manager is not null && Marshal.IsComObject(manager))
        {
            Marshal.ReleaseComObject(manager);
        }
    }

    private IThemeManager2 Manager =>
        _manager ?? throw new ObjectDisposedException(nameof(WindowsThemeActivationSession));
}
