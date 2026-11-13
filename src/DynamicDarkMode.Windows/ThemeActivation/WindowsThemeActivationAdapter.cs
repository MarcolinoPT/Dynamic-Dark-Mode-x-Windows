using System.Runtime.InteropServices;
using DynamicDarkMode.Windows.ThemeCatalog;

namespace DynamicDarkMode.Windows.ThemeActivation;

internal interface IWindowsThemeApplier
{
    Task<ThemeApplyResult> ApplyAsync(
        WindowsThemeDescriptor requestedTheme,
        CancellationToken cancellationToken = default);
}

internal enum ThemeApplyStatus
{
    RequestFailed,
    VerificationFailed,
    VerifiedSuccess
}

internal sealed record ThemeApplyResult(
    string ThemeId,
    ThemeApplyStatus Status,
    bool ApiAccepted,
    int? HResult,
    string Diagnostic);

internal sealed class WindowsThemeActivationAdapter(
    WindowsThemeCatalog catalog,
    IHighContrastStateProvider highContrastStateProvider,
    IStaExecutionContext staExecutionContext,
    IThemeActivationSessionFactory sessionFactory) : IWindowsThemeApplier
{
    private readonly SemaphoreSlim _activationGate = new(initialCount: 1, maxCount: 1);
    private readonly WindowsThemeCatalog _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly IHighContrastStateProvider _highContrastStateProvider =
        highContrastStateProvider ?? throw new ArgumentNullException(nameof(highContrastStateProvider));
    private readonly IStaExecutionContext _staExecutionContext =
        staExecutionContext ?? throw new ArgumentNullException(nameof(staExecutionContext));
    private readonly IThemeActivationSessionFactory _sessionFactory =
        sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));

    public async Task<ThemeApplyResult> ApplyAsync(
        WindowsThemeDescriptor requestedTheme,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requestedTheme);

        await _activationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(
                    function: () => ApplyOnSta(requestedTheme, cancellationToken),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _activationGate.Release();
        }
    }

    private ThemeApplyResult ApplyOnSta(
        WindowsThemeDescriptor requestedTheme,
        CancellationToken cancellationToken)
    {
        try
        {
            return _staExecutionContext.Run(
                operation: () => ApplyWithinSta(requestedTheme, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return RequestFailed(
                themeId: requestedTheme.Id,
                diagnostic: "The activation request failed before Windows accepted it.",
                exception: exception);
        }
    }

    private ThemeApplyResult ApplyWithinSta(
        WindowsThemeDescriptor requestedTheme,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IThemeActivationSession session = _sessionFactory.Create();
        WindowsThemeCatalogResult freshCatalog = _catalog.Discover(
            managerEntries: session.GetEntries());

        if (freshCatalog.HighContrastModeEnabled)
        {
            return RequestFailed(
                requestedTheme.Id,
                "Theme activation is disabled while High Contrast is enabled.");
        }

        WindowsThemeDescriptor? freshTheme = freshCatalog.Themes.SingleOrDefault(
            theme => string.Equals(theme.Id, requestedTheme.Id, StringComparison.Ordinal));

        if (freshTheme is null)
        {
            return RequestFailed(
                requestedTheme.Id,
                "The requested theme is no longer present in the current catalog.");
        }

        if (freshTheme.MatchStatus != ThemeManagerMatchStatus.Matched)
        {
            return RequestFailed(
                requestedTheme.Id,
                DescribeIneligibleTheme(freshTheme.MatchStatus));
        }

        IReadOnlyList<ThemeManagerEntry> currentEntries = session.GetEntries();
        IReadOnlyList<ThemeManagerEntry> currentMatches = currentEntries
            .Where(entry => ThemeCatalogMatcher.Matches(freshTheme, entry))
            .ToArray();

        if (currentMatches.Count == 0)
        {
            return RequestFailed(
                requestedTheme.Id,
                "The requested theme no longer matches an entry in the current COM session.");
        }

        if (currentMatches.Count > 1)
        {
            return RequestFailed(
                requestedTheme.Id,
                "The requested theme now matches multiple Windows entries and cannot be selected safely.");
        }

        ThemeManagerEntry currentMatch = currentMatches[0];
        if (currentMatch.IsDisabled)
        {
            return RequestFailed(
                requestedTheme.Id,
                "Windows marks the requested theme as disabled.");
        }

        if (_highContrastStateProvider.IsEnabled())
        {
            return RequestFailed(
                requestedTheme.Id,
                "Theme activation is disabled because High Contrast became enabled.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            session.SetCurrentTheme(currentMatch.EnumerationIndex);
        }
        catch (Exception exception)
        {
            return RequestFailed(
                themeId: requestedTheme.Id,
                diagnostic: "Windows did not accept the theme activation request.",
                exception: exception);
        }

        try
        {
            ThemeManagerEntry currentTheme = session.GetCurrentTheme();
            if (!ThemeCatalogMatcher.Matches(freshTheme, currentTheme))
            {
                return new ThemeApplyResult(
                    ThemeId: requestedTheme.Id,
                    Status: ThemeApplyStatus.VerificationFailed,
                    ApiAccepted: true,
                    HResult: null,
                    Diagnostic: "Windows accepted the request but reports a different current theme.");
            }
        }
        catch (Exception exception)
        {
            return VerificationFailed(
                themeId: requestedTheme.Id,
                diagnostic: "Windows accepted the request but the active theme could not be verified.",
                exception: exception);
        }

        return new ThemeApplyResult(
            ThemeId: requestedTheme.Id,
            Status: ThemeApplyStatus.VerifiedSuccess,
            ApiAccepted: true,
            HResult: null,
            Diagnostic: "Windows reports the requested theme as current.");
    }

    private static string DescribeIneligibleTheme(ThemeManagerMatchStatus matchStatus) =>
        matchStatus switch
        {
            ThemeManagerMatchStatus.Unmatched =>
                "The requested theme could not be matched to a current Windows theme entry.",
            ThemeManagerMatchStatus.Ambiguous =>
                "The requested theme matches multiple Windows entries and cannot be selected safely.",
            ThemeManagerMatchStatus.Disabled =>
                "Windows marks the requested theme as disabled.",
            _ => "The requested theme is not eligible for activation."
        };

    private static ThemeApplyResult RequestFailed(
        string themeId,
        string diagnostic,
        Exception? exception = null) =>
        new(
            ThemeId: themeId,
            Status: ThemeApplyStatus.RequestFailed,
            ApiAccepted: false,
            HResult: GetHResult(exception),
            Diagnostic: AddExceptionDetail(diagnostic, exception));

    private static ThemeApplyResult VerificationFailed(
        string themeId,
        string diagnostic,
        Exception exception) =>
        new(
            ThemeId: themeId,
            Status: ThemeApplyStatus.VerificationFailed,
            ApiAccepted: true,
            HResult: GetHResult(exception),
            Diagnostic: AddExceptionDetail(diagnostic, exception));

    private static int? GetHResult(Exception? exception) =>
        exception?.HResult;

    private static string AddExceptionDetail(string diagnostic, Exception? exception) =>
        exception is not null
            ? $"{diagnostic} ({exception.GetType().Name}, HRESULT 0x{exception.HResult:X8})."
            : diagnostic;
}

internal static class WindowsThemeActivationAdapterFactory
{
    public static IWindowsThemeApplier CreateDefault()
    {
        var highContrastStateProvider = new WindowsHighContrastStateProvider();
        WindowsThemeCatalog catalog = WindowsThemeCatalogFactory.CreateDefault(
            highContrastStateProvider: highContrastStateProvider);

        return new WindowsThemeActivationAdapter(
            catalog: catalog,
            highContrastStateProvider: highContrastStateProvider,
            staExecutionContext: new WindowsStaExecutionContext(),
            sessionFactory: new WindowsThemeActivationSessionFactory());
    }
}
