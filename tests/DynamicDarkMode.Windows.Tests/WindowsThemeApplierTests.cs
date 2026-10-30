using DynamicDarkMode.Core;
using DynamicDarkMode.Windows;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Windows.Tests;

public sealed class WindowsThemeApplierTests
{
    private const string SystemUsesLightTheme = "SystemUsesLightTheme";
    private const string AppsUseLightTheme = "AppsUseLightTheme";

    [Theory]
    [InlineData(ThemeAppearance.Light, 1)]
    [InlineData(ThemeAppearance.Dark, 0)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task AppliesRequestedAppearanceToBothCurrentUserSettings(
        ThemeAppearance appearance,
        int expectedValue)
    {
        // Arrange
        var settings = new FakeSettingsStore(
            systemValue: appearance == ThemeAppearance.Light ? 0 : 1,
            appsValue: appearance == ThemeAppearance.Light ? 0 : 1);
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        await applier.ApplyAsync(appearance: appearance);
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        systemValue.ShouldSatisfyAllConditions(
            () => systemValue.ShouldBe(expectedValue),
            () => appsValue.ShouldBe(expectedValue),
            () => notificationCount.ShouldBe(1));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task AlreadyActiveAppearanceDoesNotWriteOrNotify()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0);
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        await applier.ApplyAsync(appearance: ThemeAppearance.Dark);
        int writeCount = settings.WriteCount;
        int notificationCount = notifier.CallCount;

        // Assert
        writeCount.ShouldSatisfyAllConditions(
            () => writeCount.ShouldBe(0),
            () => notificationCount.ShouldBe(0));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task FailedWriteAttemptsToRestoreBothOriginalValues()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0)
        {
            FailWrite = (name, value, _) => name == AppsUseLightTheme && value == 1
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light)) as ThemeApplicationException;
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldNotBeNull(),
            () => systemValue.ShouldBe(0),
            () => appsValue.ShouldBe(0),
            () => notificationCount.ShouldBe(0));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RollbackFailureIsIncludedInDiagnostics()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0)
        {
            FailWrite = (name, value, writeCount) =>
                (name == AppsUseLightTheme && value == 1)
                || (name == SystemUsesLightTheme && value == 0 && writeCount >= 3)
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light)) as ThemeApplicationException;
        int recoveryExceptionCount = exception?.RecoveryExceptions.Count ?? 0;

        // Assert
        recoveryExceptionCount.ShouldSatisfyAllConditions(
            () => exception.ShouldNotBeNull(),
            () => recoveryExceptionCount.ShouldBeGreaterThan(0));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task NotificationFailureRestoresOriginalValuesAndReportsFailure()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0);
        var notifier = new FakeNotifier { FailFirstCall = true };
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light)) as ThemeApplicationException;
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldNotBeNull(),
            () => systemValue.ShouldBe(0),
            () => appsValue.ShouldBe(0),
            () => notificationCount.ShouldBe(2));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsUnknownAppearanceWithoutChangingSettings()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 1, appsValue: 1);
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Unknown));
        int writeCount = settings.WriteCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ArgumentOutOfRangeException>(),
            () => writeCount.ShouldBe(0));
    }

    private sealed class FakeSettingsStore(int? systemValue, int? appsValue) : IWindowsThemeSettingsStore
    {
        private readonly Dictionary<string, int?> _values = new()
        {
            [SystemUsesLightTheme] = systemValue,
            [AppsUseLightTheme] = appsValue
        };

        public Func<string, int?, int, bool>? FailWrite { get; init; }

        public int WriteCount { get; private set; }

        public int? Read(string valueName) => _values[valueName];

        public void Write(string valueName, int? value)
        {
            WriteCount++;
            if (FailWrite?.Invoke(valueName, value, WriteCount) == true)
            {
                throw new InvalidOperationException($"Simulated write failure for {valueName}.");
            }

            _values[valueName] = value;
        }
    }

    private sealed class FakeNotifier : IWindowsSettingChangeNotifier
    {
        public bool FailFirstCall { get; init; }

        public int CallCount { get; private set; }

        public void BroadcastThemeChanged()
        {
            CallCount++;
            if (FailFirstCall && CallCount == 1)
            {
                throw new InvalidOperationException("Simulated notification failure.");
            }
        }
    }
}
