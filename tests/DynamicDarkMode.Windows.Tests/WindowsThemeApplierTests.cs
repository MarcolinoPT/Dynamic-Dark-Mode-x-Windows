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
    public async Task AppliesLightToOnlyTheSettingThatDoesNotAlreadyMatch()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 1, appsValue: 0);
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        await applier.ApplyAsync(appearance: ThemeAppearance.Light);

        // Assert
        settings.WriteCount.ShouldBe(1);
        settings.Read(SystemUsesLightTheme).ShouldBe(1);
        settings.Read(AppsUseLightTheme).ShouldBe(1);
        notifier.CallCount.ShouldBe(1);
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

        // Assert
        settings.WriteCount.ShouldBe(0);
        notifier.CallCount.ShouldBe(0);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task ConcurrentApplicationsAreSerialized()
    {
        // Arrange
        using var firstWriteEntered = new ManualResetEventSlim();
        using var releaseFirstWrite = new ManualResetEventSlim();
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0)
        {
            FirstWriteEntered = firstWriteEntered,
            ReleaseFirstWrite = releaseFirstWrite
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        Task first = Task.Run(async () => await applier.ApplyAsync(ThemeAppearance.Light));
        firstWriteEntered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();

        using var secondStarted = new ManualResetEventSlim();
        Task second = Task.Run(async () =>
        {
            secondStarted.Set();
            await applier.ApplyAsync(ThemeAppearance.Dark);
        });

        try
        {
            secondStarted.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            await Task.Delay(150);

            // The second operation must not even read the settings while the
            // first operation owns the per-user mutex.
            settings.ReadCount.ShouldBe(2);
        }
        finally
        {
            releaseFirstWrite.Set();
        }

        await Task.WhenAll(first, second);

        // Assert: the second operation runs after the first and leaves a
        // consistent final appearance.
        settings.Read(SystemUsesLightTheme).ShouldBe(0);
        settings.Read(AppsUseLightTheme).ShouldBe(0);
        notifier.CallCount.ShouldBe(2);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task FailedWriteAttemptsToRestoreBothOriginalValuesAfterPartialMutation()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0)
        {
            FailAfterMutation = true,
            FailWrite = (name, value, _) => name == AppsUseLightTheme && value == 1
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light)) as ThemeApplicationException;

        // Assert
        exception.ShouldNotBeNull();
        settings.Read(SystemUsesLightTheme).ShouldBe(0);
        settings.Read(AppsUseLightTheme).ShouldBe(0);
        notifier.CallCount.ShouldBe(0);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task FailedWriteRestoresOriginallyMissingSettingAsAbsent()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: null, appsValue: 0)
        {
            FailWrite = (name, value, _) => name == AppsUseLightTheme && value == 1
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(ThemeAppearance.Light)) as ThemeApplicationException;

        // Assert
        exception.ShouldNotBeNull();
        settings.Read(SystemUsesLightTheme).ShouldBeNull();
        settings.Read(AppsUseLightTheme).ShouldBe(0);
        exception.RecoveryExceptions.ShouldBeEmpty();
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
            () => applier.ApplyAsync(ThemeAppearance.Light)) as ThemeApplicationException;

        // Assert
        exception.ShouldNotBeNull();
        exception.RecoveryExceptions.Count.ShouldBeGreaterThan(0);
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
            () => applier.ApplyAsync(ThemeAppearance.Light)) as ThemeApplicationException;

        // Assert
        exception.ShouldNotBeNull();
        settings.Read(SystemUsesLightTheme).ShouldBe(0);
        settings.Read(AppsUseLightTheme).ShouldBe(0);
        notifier.CallCount.ShouldBe(2);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task NotificationRollbackPreservesSettingsChangedByAnotherActor()
    {
        // Arrange
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0);
        var notifier = new FakeNotifier
        {
            FailFirstCall = true,
            OnFirstCall = () => settings.SetExternally(SystemUsesLightTheme, 7)
        };
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        ThemeApplicationException? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(ThemeAppearance.Light)) as ThemeApplicationException;

        // Assert
        exception.ShouldNotBeNull();
        settings.Read(SystemUsesLightTheme).ShouldBe(7);
        settings.Read(AppsUseLightTheme).ShouldBe(0);
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

        // Assert
        exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        settings.WriteCount.ShouldBe(0);
    }

    private sealed class FakeSettingsStore(int? systemValue, int? appsValue) : IWindowsThemeSettingsStore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, int?> _values = new()
        {
            [SystemUsesLightTheme] = systemValue,
            [AppsUseLightTheme] = appsValue
        };

        private int _readCount;
        private int _writeCount;

        public Func<string, int?, int, bool>? FailWrite { get; init; }

        public bool FailAfterMutation { get; init; }

        public ManualResetEventSlim? FirstWriteEntered { get; init; }

        public ManualResetEventSlim? ReleaseFirstWrite { get; init; }

        public int ReadCount => Volatile.Read(ref _readCount);

        public int WriteCount => Volatile.Read(ref _writeCount);

        public int? Read(string valueName)
        {
            Interlocked.Increment(ref _readCount);
            lock (_gate)
            {
                return _values[valueName];
            }
        }

        public void Write(string valueName, int? value)
        {
            int writeCount = Interlocked.Increment(ref _writeCount);
            if (writeCount == 1 && FirstWriteEntered is not null && ReleaseFirstWrite is not null)
            {
                FirstWriteEntered.Set();
                ReleaseFirstWrite.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            }

            bool shouldFail = FailWrite?.Invoke(valueName, value, writeCount) == true;
            if (shouldFail && !FailAfterMutation)
            {
                throw new InvalidOperationException($"Simulated write failure for {valueName}.");
            }

            lock (_gate)
            {
                _values[valueName] = value;
            }

            if (shouldFail && FailAfterMutation)
            {
                throw new InvalidOperationException($"Simulated write failure for {valueName}.");
            }
        }

        public void SetExternally(string valueName, int? value)
        {
            lock (_gate)
            {
                _values[valueName] = value;
            }
        }
    }

    private sealed class FakeNotifier : IWindowsSettingChangeNotifier
    {
        public bool FailFirstCall { get; init; }

        public Action? OnFirstCall { get; init; }

        public int CallCount { get; private set; }

        public void BroadcastThemeChanged()
        {
            CallCount++;
            if (CallCount == 1)
            {
                OnFirstCall?.Invoke();
                if (FailFirstCall)
                {
                    throw new InvalidOperationException("Simulated notification failure.");
                }
            }
        }
    }
}
