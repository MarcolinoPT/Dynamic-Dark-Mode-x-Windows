namespace DynamicDarkMode.Windows.ThemeCatalog;

internal sealed class WindowsThemeCatalog(
    WindowsThemeFileDiscovery fileDiscovery,
    ThemeFileParser fileParser,
    IThemeManagerEntrySource managerEntrySource,
    IHighContrastStateProvider highContrastStateProvider)
{
    private readonly WindowsThemeFileDiscovery _fileDiscovery =
        fileDiscovery ?? throw new ArgumentNullException(nameof(fileDiscovery));
    private readonly ThemeFileParser _fileParser =
        fileParser ?? throw new ArgumentNullException(nameof(fileParser));
    private readonly IThemeManagerEntrySource _managerEntrySource =
        managerEntrySource ?? throw new ArgumentNullException(nameof(managerEntrySource));
    private readonly IHighContrastStateProvider _highContrastStateProvider =
        highContrastStateProvider ?? throw new ArgumentNullException(nameof(highContrastStateProvider));

    /// <summary>Discovers theme packages, correlates them with Windows, and captures accessibility state.</summary>
    /// <returns>The discovered descriptors, High Contrast snapshot, and skipped-file count.</returns>
    public WindowsThemeCatalogResult Discover()
    {
        bool highContrastModeEnabled = _highContrastStateProvider.IsEnabled();
        IReadOnlyList<ThemeManagerEntry> managerEntries = _managerEntrySource.GetEntries()
            .Where(entry => !IsHighContrastVisualStyle(entry.VisualStylePath))
            .ToArray();

        var parsedThemeFiles = new List<ThemeFileMetadata>();
        int skippedFileCount = 0;

        foreach (string filePath in _fileDiscovery.EnumerateThemeFiles())
        {
            try
            {
                ThemeFileMetadata metadata = _fileParser.Parse(filePath);
                if (_fileDiscovery.IsHighContrastTheme(metadata))
                {
                    continue;
                }

                parsedThemeFiles.Add(metadata);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or ArgumentException
                    or NotSupportedException
                    or FormatException)
            {
                skippedFileCount++;
            }
        }

        IReadOnlyList<WindowsThemeDescriptor> descriptors = ThemeCatalogMatcher.Match(
            themeFiles: parsedThemeFiles,
            managerEntries: managerEntries,
            isBuiltInTheme: _fileDiscovery.IsBuiltInTheme);

        return new WindowsThemeCatalogResult(
            Themes: descriptors,
            HighContrastModeEnabled: highContrastModeEnabled,
            SkippedFileCount: skippedFileCount);
    }

    private static bool IsHighContrastVisualStyle(string? visualStylePath) =>
        !string.IsNullOrWhiteSpace(visualStylePath)
        && string.Equals(
            Path.GetFileName(visualStylePath),
            "AeroLite.msstyles",
            StringComparison.OrdinalIgnoreCase);
}
