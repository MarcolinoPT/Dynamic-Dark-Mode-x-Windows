using DynamicDarkMode.Core;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Core.Tests;

public sealed class ThemeAppearanceTests
{
    [Fact]
    public void DefaultAppearance_IsUnknown()
    {
        default(ThemeAppearance).ShouldBe(ThemeAppearance.Unknown);
    }

    [Fact]
    public void Appearance_ExposesExplicitValuesIncludingUnknown()
    {
        Enum.GetValues<ThemeAppearance>()
            .ShouldBe(new[]
            {
                ThemeAppearance.Unknown,
                ThemeAppearance.Light,
                ThemeAppearance.Dark
            });

        ((int)ThemeAppearance.Unknown).ShouldBe(0);
        ((int)ThemeAppearance.Light).ShouldBe(1);
        ((int)ThemeAppearance.Dark).ShouldBe(2);
    }
}
