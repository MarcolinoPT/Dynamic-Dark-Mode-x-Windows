namespace DynamicDarkMode.Windows.ThemeCatalog;

internal static class WindowsThemeCatalogFactory
{
    public static WindowsThemeCatalog CreateDefault()
    {
        WindowsThemeSearchPaths searchPaths = WindowsThemeSearchPaths.CreateDefault();

        return new WindowsThemeCatalog(
            fileDiscovery: new WindowsThemeFileDiscovery(searchPaths),
            fileParser: new ThemeFileParser(new WindowsIndirectStringResolver()),
            managerEntrySource: new WindowsThemeManagerEntrySource(),
            highContrastStateProvider: new WindowsHighContrastStateProvider());
    }
}
