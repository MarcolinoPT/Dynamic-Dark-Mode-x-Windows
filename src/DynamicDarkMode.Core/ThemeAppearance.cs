namespace DynamicDarkMode.Core;

/// <summary>
/// The effective appearance that the Windows companion may apply.
/// This model is platform-neutral so it can be tested without Windows APIs.
/// </summary>
public enum ThemeAppearance
{
    Unknown = 0,
    Light = 1,
    Dark = 2
}
