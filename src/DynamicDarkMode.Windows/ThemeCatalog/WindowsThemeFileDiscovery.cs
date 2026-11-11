namespace DynamicDarkMode.Windows.ThemeCatalog;

internal sealed record WindowsThemeSearchPaths(
    string WindowsDirectory,
    string SystemThemesDirectory,
    string EaseOfAccessThemesDirectory,
    string SharedThemesDirectory,
    string UserThemesDirectory)
{
    public static WindowsThemeSearchPaths CreateDefault()
    {
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string commonApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData);

        if (string.IsNullOrWhiteSpace(windowsDirectory)
            || string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "Windows theme discovery directories could not be resolved.");
        }

        return new WindowsThemeSearchPaths(
            WindowsDirectory: windowsDirectory,
            SystemThemesDirectory: Path.Combine(windowsDirectory, "Resources", "Themes"),
            EaseOfAccessThemesDirectory: Path.Combine(
                windowsDirectory,
                "Resources",
                "Ease of Access Themes"),
            SharedThemesDirectory: Path.Combine(
                commonApplicationData,
                "Microsoft",
                "Windows",
                "Themes"),
            UserThemesDirectory: Path.Combine(
                localApplicationData,
                "Microsoft",
                "Windows",
                "Themes"));
    }
}

internal sealed class WindowsThemeFileDiscovery(WindowsThemeSearchPaths searchPaths)
{
    private readonly WindowsThemeSearchPaths _searchPaths =
        searchPaths ?? throw new ArgumentNullException(nameof(searchPaths));

    public IReadOnlyList<string> EnumerateThemeFiles()
    {
        string[] roots =
        [
            _searchPaths.SystemThemesDirectory,
            _searchPaths.SharedThemesDirectory,
            _searchPaths.UserThemesDirectory
        ];

        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                foreach (string filePath in Directory.EnumerateFiles(
                    root,
                    "*.theme",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false
                    }))
                {
                    try
                    {
                        discovered.Add(Path.GetFullPath(filePath));
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException
                            or NotSupportedException
                            or PathTooLongException)
                    {
                        // Ignore malformed filesystem entries; discovery is best-effort.
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException)
            {
                // An inaccessible theme root must not prevent other roots from being scanned.
            }
        }

        return discovered.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public bool IsHighContrastTheme(ThemeFileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return IsWithinDirectory(
                path: metadata.FilePath,
                directory: _searchPaths.EaseOfAccessThemesDirectory)
            || IsAeroLiteStyle(metadata.VisualStylePath);
    }

    public bool IsBuiltInTheme(ThemeFileMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return IsWithinDirectory(
            path: metadata.FilePath,
            directory: _searchPaths.SystemThemesDirectory);
    }

    private static bool IsAeroLiteStyle(string? visualStylePath) =>
        !string.IsNullOrWhiteSpace(visualStylePath)
        && string.Equals(
            Path.GetFileName(visualStylePath),
            "AeroLite.msstyles",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsWithinDirectory(string path, string directory)
    {
        string fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.StartsWith(
            fullDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
