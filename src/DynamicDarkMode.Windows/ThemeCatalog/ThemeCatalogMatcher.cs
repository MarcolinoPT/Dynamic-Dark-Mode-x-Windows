using System.Security.Cryptography;
using System.Text;

namespace DynamicDarkMode.Windows.ThemeCatalog;

internal static class ThemeCatalogMatcher
{
    /// <summary>Correlates parsed theme packages with the current Windows theme-manager entries.</summary>
    /// <param name="themeFiles">Parsed theme packages to correlate.</param>
    /// <param name="managerEntries">Entries from the current theme-manager enumeration.</param>
    /// <param name="isBuiltInTheme">Predicate identifying packages in the built-in Windows theme directory.</param>
    /// <returns>Descriptors with stable file identities and explicit match statuses.</returns>
    public static IReadOnlyList<WindowsThemeDescriptor> Match(
        IReadOnlyList<ThemeFileMetadata> themeFiles,
        IReadOnlyList<ThemeManagerEntry> managerEntries,
        Func<ThemeFileMetadata, bool> isBuiltInTheme)
    {
        ArgumentNullException.ThrowIfNull(themeFiles);
        ArgumentNullException.ThrowIfNull(managerEntries);
        ArgumentNullException.ThrowIfNull(isBuiltInTheme);

        var candidateMatches = new List<IReadOnlyList<ThemeManagerEntry>>(themeFiles.Count);
        var filesByManagerIndex = new Dictionary<int, HashSet<int>>();

        for (int fileIndex = 0; fileIndex < themeFiles.Count; fileIndex++)
        {
            IReadOnlyList<ThemeManagerEntry> matches = FindMatches(
                themeFiles[fileIndex],
                managerEntries);
            candidateMatches.Add(matches);

            foreach (ThemeManagerEntry match in matches)
            {
                if (!filesByManagerIndex.TryGetValue(
                    match.EnumerationIndex,
                    out HashSet<int>? matchingFiles))
                {
                    matchingFiles = [];
                    filesByManagerIndex.Add(match.EnumerationIndex, matchingFiles);
                }

                matchingFiles.Add(fileIndex);
            }
        }

        var descriptors = new List<WindowsThemeDescriptor>(themeFiles.Count);
        for (int index = 0; index < themeFiles.Count; index++)
        {
            ThemeFileMetadata themeFile = themeFiles[index];
            IReadOnlyList<ThemeManagerEntry> matches = candidateMatches[index];

            ThemeManagerMatchStatus matchStatus = matches.Count switch
            {
                0 => ThemeManagerMatchStatus.Unmatched,
                > 1 => ThemeManagerMatchStatus.Ambiguous,
                _ when matches.Any(match =>
                    filesByManagerIndex[match.EnumerationIndex].Count > 1)
                        => ThemeManagerMatchStatus.Ambiguous,
                _ when matches[0].IsDisabled => ThemeManagerMatchStatus.Disabled,
                _ => ThemeManagerMatchStatus.Matched
            };

            descriptors.Add(new WindowsThemeDescriptor(
                Id: ThemeIdentity.Create(themeFile.FilePath),
                DisplayName: themeFile.DisplayName,
                FilePath: themeFile.FilePath,
                IsDisplayNameResolved: themeFile.IsDisplayNameResolved,
                UnresolvedDisplayNameResource: themeFile.UnresolvedDisplayNameResource,
                VisualStylePath: themeFile.VisualStylePath,
                ColorStyle: themeFile.ColorStyle,
                Size: themeFile.Size,
                IsBuiltIn: isBuiltInTheme(themeFile),
                MatchStatus: matchStatus));
        }

        return descriptors;
    }

    private static IReadOnlyList<ThemeManagerEntry> FindMatches(
        ThemeFileMetadata themeFile,
        IReadOnlyList<ThemeManagerEntry> managerEntries)
    {
        if (!themeFile.IsDisplayNameResolved
            || themeFile.UnresolvedDisplayNameResource is not null
            || string.IsNullOrWhiteSpace(themeFile.DisplayName)
            || string.IsNullOrWhiteSpace(themeFile.VisualStylePath)
            || string.IsNullOrWhiteSpace(themeFile.ColorStyle))
        {
            return [];
        }

        string? themeVisualStylePath = NormalizePath(themeFile.VisualStylePath);
        if (themeVisualStylePath is null)
        {
            return [];
        }

        return managerEntries
            .Where(entry =>
                string.Equals(
                    NormalizeDisplayName(entry.DisplayName),
                    NormalizeDisplayName(themeFile.DisplayName),
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    NormalizePath(entry.VisualStylePath),
                    themeVisualStylePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    entry.ColorStyle.Trim(),
                    themeFile.ColorStyle.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string NormalizeDisplayName(string value) => value.Trim();

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        if (!Path.IsPathRooted(expanded))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(expanded)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return null;
        }
    }
}

internal static class ThemeIdentity
{
    /// <summary>Creates a versioned identity from a theme package's canonical absolute path.</summary>
    /// <param name="themeFilePath">Path to the theme package.</param>
    /// <returns>A <c>theme-file-v1:</c> identifier containing the SHA-256 path hash.</returns>
    public static string Create(string themeFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(themeFilePath);

        string canonicalPath = Path.GetFullPath(themeFilePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        return $"theme-file-v1:{Convert.ToHexString(hash)}";
    }
}
