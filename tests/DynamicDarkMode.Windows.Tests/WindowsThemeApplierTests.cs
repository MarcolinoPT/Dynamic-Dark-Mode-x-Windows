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
        int writeCount = settings.WriteCount;
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        systemValue.ShouldSatisfyAllConditions(
            () => writeCount.ShouldBe(1),
            () => systemValue.ShouldBe(1),
            () => appsValue.ShouldBe(1),
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
    public async Task ConcurrentApplicationsAreSerialized()
    {
        // Arrange
        using var firstWriteEntered = new ManualResetEventSlim();
        using var releaseFirstWrite = new ManualResetEventSlim();
        using var secondReadEntered = new ManualResetEventSlim();
        var settings = new FakeSettingsStore(systemValue: 0, appsValue: 0)
        {
            FirstWriteEntered = firstWriteEntered,
            ReleaseFirstWrite = releaseFirstWrite,
            SecondReadEntered = secondReadEntered
        };
        var notifier = new FakeNotifier();
        var applier = new WindowsThemeApplier(settingsStore: settings, notifier: notifier);

        // Act
        Task first = Task.Run(async () => await applier.ApplyAsync(ThemeAppearance.Light));
        bool firstWriteWasEntered = firstWriteEntered.Wait(TimeSpan.FromSeconds(5));

        using var secondStarted = new ManualResetEventSlim();
        Task second = Task.Run(async () =>
        {
            secondStarted.Set();
            await applier.ApplyAsync(ThemeAppearance.Dark);
        });

        bool secondWasStarted;
        bool secondReadWhileFirstHeldMutex;
        try
        {
            secondWasStarted = secondStarted.Wait(TimeSpan.FromSeconds(5));
            secondReadWhileFirstHeldMutex = secondReadEntered.Wait(TimeSpan.FromMilliseconds(150));
        }
        finally
        {
            releaseFirstWrite.Set();
        }

        Exception? operationException = await Record.ExceptionAsync(async () => await Task.WhenAll(first, second));
        int? finalSystemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? finalAppsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;
        bool firstWriteWaitSucceeded = settings.FirstWriteWaitSucceeded;

        // Assert
        operationException.ShouldSatisfyAllConditions(
            () => operationException.ShouldBeNull(),
            () => firstWriteWasEntered.ShouldBeTrue(),
            () => secondWasStarted.ShouldBeTrue(),
            () => secondReadWhileFirstHeldMutex.ShouldBeFalse(),
            () => firstWriteWaitSucceeded.ShouldBeTrue(),
            () => finalSystemValue.ShouldBe(0),
            () => finalAppsValue.ShouldBe(0),
            () => notificationCount.ShouldBe(2));
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
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light));
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ThemeApplicationException>(),
            () => systemValue.ShouldBe(0),
            () => appsValue.ShouldBe(0),
            () => notificationCount.ShouldBe(0));
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
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light));
        ThemeApplicationException? themeException = exception as ThemeApplicationException;
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        IReadOnlyCollection<Exception>? recoveryExceptions = themeException?.RecoveryExceptions;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ThemeApplicationException>(),
            () => systemValue.ShouldBeNull(),
            () => appsValue.ShouldBe(0),
            () => recoveryExceptions.ShouldBeEmpty());
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
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light));
        ThemeApplicationException? themeException = exception as ThemeApplicationException;
        IReadOnlyCollection<Exception>? recoveryExceptions = themeException?.RecoveryExceptions;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ThemeApplicationException>(),
            () => recoveryExceptions.ShouldNotBeNull(),
            () => recoveryExceptions!.Count.ShouldBeGreaterThan(0));
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
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light));
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);
        int notificationCount = notifier.CallCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ThemeApplicationException>(),
            () => systemValue.ShouldBe(0),
            () => appsValue.ShouldBe(0),
            () => notificationCount.ShouldBe(2));
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
        Exception? exception = await Record.ExceptionAsync(
            () => applier.ApplyAsync(appearance: ThemeAppearance.Light));
        int? systemValue = settings.Read(valueName: SystemUsesLightTheme);
        int? appsValue = settings.Read(valueName: AppsUseLightTheme);

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ThemeApplicationException>(),
            () => systemValue.ShouldBe(7),
            () => appsValue.ShouldBe(0));
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
        int notificationCount = notifier.CallCount;

        // Assert
        exception.ShouldSatisfyAllConditions(
            () => exception.ShouldBeOfType<ArgumentOutOfRangeException>(),
            () => writeCount.ShouldBe(0),
            () => notificationCount.ShouldBe(0));
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

        public ManualResetEventSlim? SecondReadEntered { get; init; }

        public bool FirstWriteWaitSucceeded { get; private set; }

        public int ReadCount => Volatile.Read(ref _readCount);

        public int WriteCount => Volatile.Read(ref _writeCount);

        public int? Read(string valueName)
        {
            int readCount = Interlocked.Increment(ref _readCount);
            if (readCount > 2)
            {
                SecondReadEntered?.Set();
            }

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
                FirstWriteWaitSucceeded = ReleaseFirstWrite.Wait(TimeSpan.FromSeconds(5));
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
