namespace DynamicDarkMode.Windows.ThemeCatalog;

internal static class WindowsThemeCatalogFactory
{
    /// <summary>Creates a catalog wired to the current Windows filesystem and native APIs.</summary>
    /// <returns>A ready-to-use Windows theme catalog.</returns>
    public static WindowsThemeCatalog CreateDefault() =>
        CreateDefault(highContrastStateProvider: new WindowsHighContrastStateProvider());

    internal static WindowsThemeCatalog CreateDefault(
        IHighContrastStateProvider highContrastStateProvider)
    {
        ArgumentNullException.ThrowIfNull(highContrastStateProvider);
        WindowsThemeSearchPaths searchPaths = WindowsThemeSearchPaths.CreateDefault();

        return new WindowsThemeCatalog(
            fileDiscovery: new WindowsThemeFileDiscovery(searchPaths),
            fileParser: new ThemeFileParser(new WindowsIndirectStringResolver()),
            managerEntrySource: new WindowsThemeManagerEntrySource(),
            highContrastStateProvider: highContrastStateProvider);
    }
}
