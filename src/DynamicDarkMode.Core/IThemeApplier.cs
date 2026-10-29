namespace DynamicDarkMode.Core;

/// <summary>
/// Applies an appearance to the current Windows user's environment.
/// Implementations belong in the Windows-specific layer.
/// </summary>
public interface IThemeApplier
{
    Task ApplyAsync(
        ThemeAppearance appearance,
        CancellationToken cancellationToken = default);
}
