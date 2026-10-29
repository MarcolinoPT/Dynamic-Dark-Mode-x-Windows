using DynamicDarkMode.Core;

namespace DynamicDarkMode.Core.Tests;

public sealed class ThemeAppearanceTests
{
    [Fact]
    public void Appearance_ExposesOnlySupportedInitialValues()
    {
        Assert.Equal(
            new[] { ThemeAppearance.Light, ThemeAppearance.Dark },
            Enum.GetValues<ThemeAppearance>());
    }
}
