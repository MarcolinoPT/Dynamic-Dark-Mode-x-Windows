using System.Runtime.InteropServices;
using DynamicDarkMode.Windows.ThemeActivation;
using DynamicDarkMode.Windows.ThemeCatalog;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Windows.Tests;

public sealed class WindowsThemeActivationAdapterTests
{
    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task AppliesFreshCatalogIndexAndVerifiesSuccess()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 3)],
            entriesForActivation: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0),
            verifiedCurrentThemeIndex: 12);
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.VerifiedSuccess),
            () => result.ApiAccepted.ShouldBeTrue(),
            () => result.HResult.ShouldBeNull(),
            () => result.ThemeId.ShouldBe(requestedTheme.Id),
            () => result.ThemeId.ShouldStartWith("theme-file-v1:"),
            () => session.AppliedThemeIndex.ShouldBe(6),
            () => session.CurrentTheme.DisplayName.ShouldBe("Sample"),
            () => session.CurrentTheme.EnumerationIndex.ShouldBe(12),
            () => session.GetEntriesCallCount.ShouldBe(2),
            () => session.IsDisposed.ShouldBeTrue());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsMissingThemeWithoutSelectingAnotherEntry()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2)) with { Id = "theme-file-v1:missing" };
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.Diagnostic.ShouldContain("no longer present"),
            () => session.AppliedThemeIndex.ShouldBeNull(),
            () => session.IsDisposed.ShouldBeTrue());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsDisabledTheme()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 3)],
            entriesForActivation: [fixture.CreateManagerEntry(index: 6, isDisabled: true)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.Diagnostic.ShouldContain("disabled"),
            () => session.AppliedThemeIndex.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsAmbiguousTheme()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 3)],
            entriesForActivation:
            [
                fixture.CreateManagerEntry(index: 6),
                fixture.CreateManagerEntry(index: 9)
            ],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.Diagnostic.ShouldContain("multiple Windows entries"),
            () => session.AppliedThemeIndex.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsTargetThatDisappearsBeforeActivation()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 3)],
            entriesForActivation: [],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.Diagnostic.ShouldContain("no longer matches"),
            () => session.AppliedThemeIndex.ShouldBeNull(),
            () => session.GetEntriesCallCount.ShouldBe(2));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task RejectsActivationWhenHighContrastBecomesEnabled()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false, true);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.Diagnostic.ShouldContain("became enabled"),
            () => session.AppliedThemeIndex.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task SurfacesHResultWhenWindowsRejectsActivationRequest()
    {
        // Arrange
        const int expectedHResult = unchecked((int)0x80070005);
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0),
            applyException: new COMException(
                message: "Rejected by fake manager.",
                errorCode: expectedHResult));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.HResult.ShouldBe(expectedHResult),
            () => result.Diagnostic.ShouldContain("HRESULT 0x80070005"),
            () => session.AppliedThemeIndex.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task SurfacesHResultWhenInteropMapsActivationFailureToStandardException()
    {
        // Arrange
        var mappedFailure = new UnauthorizedAccessException("Rejected by fake manager.");
        int expectedHResult = mappedFailure.HResult;
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0),
            applyException: mappedFailure);
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.RequestFailed),
            () => result.ApiAccepted.ShouldBeFalse(),
            () => result.HResult.ShouldBe(expectedHResult),
            () => result.Diagnostic.ShouldContain($"HRESULT 0x{expectedHResult:X8}"),
            () => session.AppliedThemeIndex.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task ReportsVerificationFailureWhenWindowsKeepsAnotherCurrentTheme()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0),
            updateCurrentThemeOnApply: false);
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.VerificationFailed),
            () => result.ApiAccepted.ShouldBeTrue(),
            () => result.HResult.ShouldBeNull(),
            () => result.Diagnostic.ShouldContain("different current theme"),
            () => session.AppliedThemeIndex.ShouldBe(6));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task SurfacesHResultWhenCurrentThemeCannotBeVerified()
    {
        // Arrange
        const int expectedHResult = unchecked((int)0x80004005);
        var highContrastProvider = new SequenceHighContrastStateProvider(false, false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var session = new FakeThemeActivationSession(
            entries: [fixture.CreateManagerEntry(index: 6)],
            currentTheme: fixture.CreateOtherManagerEntry(index: 0),
            verificationException: new COMException(
                message: "Verification failed.",
                errorCode: expectedHResult));
        IWindowsThemeApplier applier = fixture.CreateApplier(session, highContrastProvider);

        // Act
        ThemeApplyResult result = await applier.ApplyAsync(requestedTheme);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Status.ShouldBe(ThemeApplyStatus.VerificationFailed),
            () => result.ApiAccepted.ShouldBeTrue(),
            () => result.HResult.ShouldBe(expectedHResult),
            () => result.Diagnostic.ShouldContain("HRESULT 0x80004005"),
            () => session.AppliedThemeIndex.ShouldBe(6));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public async Task SerializesOverlappingActivationRequests()
    {
        // Arrange
        var highContrastProvider = new SequenceHighContrastStateProvider(
            false,
            false,
            false,
            false);
        using var fixture = new ThemeFixture(highContrastProvider);
        WindowsThemeDescriptor requestedTheme = fixture.CreateDescriptor(
            fixture.CreateManagerEntry(index: 2));
        var staExecutionContext = new BlockingStaExecutionContext();
        var sessionFactory = new MultipleThemeActivationSessionFactory(
            createSession: () => new FakeThemeActivationSession(
                entries: [fixture.CreateManagerEntry(index: 6)],
                currentTheme: fixture.CreateOtherManagerEntry(index: 0)));
        var applier = new WindowsThemeActivationAdapter(
            catalog: fixture.Catalog,
            highContrastStateProvider: highContrastProvider,
            staExecutionContext: staExecutionContext,
            sessionFactory: sessionFactory);

        // Act
        Task<ThemeApplyResult> firstRequest = applier.ApplyAsync(requestedTheme);
        await staExecutionContext.FirstOperationEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<ThemeApplyResult> secondRequest = applier.ApplyAsync(requestedTheme);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        bool secondRequestEnteredBeforeFirstCompleted =
            staExecutionContext.SecondOperationEntered.Task.IsCompleted;
        staExecutionContext.ReleaseFirstOperation();
        ThemeApplyResult[] results = await Task.WhenAll(firstRequest, secondRequest);

        // Assert
        results.ShouldSatisfyAllConditions(
            () => results.Length.ShouldBe(2),
            () => results.All(result => result.Status == ThemeApplyStatus.VerifiedSuccess).ShouldBeTrue(),
            () => secondRequestEnteredBeforeFirstCompleted.ShouldBeFalse(),
            () => staExecutionContext.MaximumConcurrentOperations.ShouldBe(1),
            () => sessionFactory.CreateCount.ShouldBe(2));
    }

    private sealed class ThemeFixture : IDisposable
    {
        private readonly string _root;
        private readonly string _visualStylePath;
        private readonly IHighContrastStateProvider _highContrastStateProvider;

        public ThemeFixture(IHighContrastStateProvider highContrastStateProvider)
        {
            _highContrastStateProvider = highContrastStateProvider;
            _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string windowsDirectory = Path.Combine(_root, "Windows");
            string systemThemesDirectory = Path.Combine(
                windowsDirectory,
                "Resources",
                "Themes");
            _visualStylePath = Path.Combine(systemThemesDirectory, "Aero.msstyles");
            Directory.CreateDirectory(systemThemesDirectory);

            string themeFilePath = Path.Combine(systemThemesDirectory, "Sample.theme");
            File.WriteAllText(
                themeFilePath,
                $"[Theme]{Environment.NewLine}DisplayName=Sample{Environment.NewLine}" +
                $"[VisualStyles]{Environment.NewLine}Path={_visualStylePath}{Environment.NewLine}" +
                "ColorStyle=NormalColor");

            var searchPaths = new WindowsThemeSearchPaths(
                WindowsDirectory: windowsDirectory,
                SystemThemesDirectory: systemThemesDirectory,
                EaseOfAccessThemesDirectory: Path.Combine(
                    windowsDirectory,
                    "Resources",
                    "Ease of Access Themes"),
                SharedThemesDirectory: Path.Combine(
                    _root,
                    "ProgramData",
                    "Microsoft",
                    "Windows",
                    "Themes"),
                UserThemesDirectory: Path.Combine(
                    _root,
                    "Users",
                    "Test",
                    "AppData",
                    "Local",
                    "Microsoft",
                    "Windows",
                    "Themes"));

            Catalog = new WindowsThemeCatalog(
                fileDiscovery: new WindowsThemeFileDiscovery(searchPaths),
                fileParser: new ThemeFileParser(new PassthroughIndirectStringResolver()),
                managerEntrySource: new FixedThemeManagerEntrySource(),
                highContrastStateProvider: highContrastStateProvider);
        }

        public WindowsThemeCatalog Catalog { get; }

        public WindowsThemeDescriptor CreateDescriptor(ThemeManagerEntry entry) =>
            Catalog.Discover(managerEntries: [entry]).Themes.Single();

        public ThemeManagerEntry CreateManagerEntry(int index, bool isDisabled = false) =>
            new(
                EnumerationIndex: index,
                DisplayName: "Sample",
                VisualStylePath: _visualStylePath,
                ColorStyle: "NormalColor",
                IsDisabled: isDisabled);

        public ThemeManagerEntry CreateOtherManagerEntry(int index) =>
            CreateManagerEntry(index) with { DisplayName = "Previous theme" };

        public IWindowsThemeApplier CreateApplier(
            FakeThemeActivationSession session,
            IHighContrastStateProvider highContrastStateProvider) =>
            new WindowsThemeActivationAdapter(
                catalog: Catalog,
                highContrastStateProvider: highContrastStateProvider,
                staExecutionContext: new InlineStaExecutionContext(),
                sessionFactory: new FixedThemeActivationSessionFactory(session));

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }

    private sealed class FakeThemeActivationSession(
        IReadOnlyList<ThemeManagerEntry> entries,
        ThemeManagerEntry currentTheme,
        IReadOnlyList<ThemeManagerEntry>? entriesForActivation = null,
        bool updateCurrentThemeOnApply = true,
        int? verifiedCurrentThemeIndex = null,
        Exception? applyException = null,
        Exception? verificationException = null) : IThemeActivationSession
    {
        public int? AppliedThemeIndex { get; private set; }

        public ThemeManagerEntry CurrentTheme { get; private set; } = currentTheme;

        public int GetEntriesCallCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public IReadOnlyList<ThemeManagerEntry> GetEntries()
        {
            GetEntriesCallCount++;
            return GetEntriesCallCount == 1
                ? entries
                : entriesForActivation ?? entries;
        }

        public void SetCurrentTheme(int themeIndex)
        {
            if (applyException is not null)
            {
                throw applyException;
            }

            AppliedThemeIndex = themeIndex;
            if (updateCurrentThemeOnApply)
            {
                ThemeManagerEntry activatedEntry = (entriesForActivation ?? entries)
                    .Single(entry => entry.EnumerationIndex == themeIndex);
                CurrentTheme = verifiedCurrentThemeIndex is int verificationIndex
                    ? activatedEntry with { EnumerationIndex = verificationIndex }
                    : activatedEntry;
            }
        }

        public ThemeManagerEntry GetCurrentTheme()
        {
            if (verificationException is not null)
            {
                throw verificationException;
            }

            return CurrentTheme;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FixedThemeActivationSessionFactory(
        IThemeActivationSession session) : IThemeActivationSessionFactory
    {
        public IThemeActivationSession Create() => session;
    }

    private sealed class MultipleThemeActivationSessionFactory(
        Func<IThemeActivationSession> createSession) : IThemeActivationSessionFactory
    {
        private int _createCount;

        public int CreateCount => _createCount;

        public IThemeActivationSession Create()
        {
            Interlocked.Increment(ref _createCount);
            return createSession();
        }
    }

    private sealed class BlockingStaExecutionContext : IStaExecutionContext
    {
        private readonly ManualResetEventSlim _releaseFirstOperation = new(initialState: false);
        private int _activeOperations;
        private int _maximumConcurrentOperations;
        private int _operationCount;

        public TaskCompletionSource FirstOperationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondOperationEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaximumConcurrentOperations => _maximumConcurrentOperations;

        public void ReleaseFirstOperation() => _releaseFirstOperation.Set();

        public T Run<T>(Func<T> operation)
        {
            int operationNumber = Interlocked.Increment(ref _operationCount);
            int activeOperations = Interlocked.Increment(ref _activeOperations);
            UpdateMaximumConcurrentOperations(activeOperations);

            try
            {
                if (operationNumber == 1)
                {
                    FirstOperationEntered.TrySetResult();
                    if (!_releaseFirstOperation.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("The first activation operation was not released.");
                    }
                }
                else if (operationNumber == 2)
                {
                    SecondOperationEntered.TrySetResult();
                }

                return operation();
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        private void UpdateMaximumConcurrentOperations(int value)
        {
            int currentMaximum = _maximumConcurrentOperations;
            while (value > currentMaximum)
            {
                int observedMaximum = Interlocked.CompareExchange(
                    ref _maximumConcurrentOperations,
                    value,
                    currentMaximum);
                if (observedMaximum == currentMaximum)
                {
                    return;
                }

                currentMaximum = observedMaximum;
            }
        }
    }

    private sealed class InlineStaExecutionContext : IStaExecutionContext
    {
        public T Run<T>(Func<T> operation) => operation();
    }

    private sealed class SequenceHighContrastStateProvider : IHighContrastStateProvider
    {
        private readonly bool[] _states;
        private int _index;

        public SequenceHighContrastStateProvider(params bool[] states)
        {
            if (states.Length == 0)
            {
                throw new ArgumentException(
                    "At least one High Contrast state is required.",
                    nameof(states));
            }

            _states = states;
        }

        public bool IsEnabled()
        {
            int stateIndex = Math.Min(_index, _states.Length - 1);
            _index++;
            return _states[stateIndex];
        }
    }

    private sealed class FixedThemeManagerEntrySource : IThemeManagerEntrySource
    {
        public IReadOnlyList<ThemeManagerEntry> GetEntries() => [];
    }

    private sealed class PassthroughIndirectStringResolver : IIndirectStringResolver
    {
        public string? Resolve(string resourceReference) => resourceReference;
    }
}
