namespace DynamicDarkMode.Windows.ThemeCatalog;

internal sealed record ThemeFileMetadata(
    string FilePath,
    string DisplayName,
    bool IsDisplayNameResolved,
    string? UnresolvedDisplayNameResource,
    string? VisualStylePath,
    string? ColorStyle,
    string? Size);

internal enum ThemeManagerMatchStatus
{
    Matched,
    Unmatched,
    Ambiguous,
    Disabled
}

internal sealed record WindowsThemeDescriptor(
    string Id,
    string DisplayName,
    string FilePath,
    bool IsDisplayNameResolved,
    string? UnresolvedDisplayNameResource,
    string? VisualStylePath,
    string? ColorStyle,
    string? Size,
    bool IsBuiltIn,
    ThemeManagerMatchStatus MatchStatus);

internal sealed record WindowsThemeCatalogResult(
    IReadOnlyList<WindowsThemeDescriptor> Themes,
    bool HighContrastModeEnabled,
    int SkippedFileCount);
