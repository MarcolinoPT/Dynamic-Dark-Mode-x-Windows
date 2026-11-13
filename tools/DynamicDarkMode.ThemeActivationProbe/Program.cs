using DynamicDarkMode.Windows.ThemeActivation;
using DynamicDarkMode.Windows.ThemeCatalog;

namespace DynamicDarkMode.ThemeActivationProbe;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The theme activation probe can only run on Windows.");
            return 2;
        }

        if (args.Length == 1 && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return ListThemes();
        }

        if (args.Length == 2 && string.Equals(args[0], "activate", StringComparison.OrdinalIgnoreCase))
        {
            return await ActivateThemeAsync(args[1]);
        }

        WriteUsage();
        return args.Length == 0 ? 0 : 2;
    }

    private static int ListThemes()
    {
        try
        {
            WindowsThemeCatalogResult catalog = WindowsThemeCatalogFactory.CreateDefault().Discover();
            WriteCatalog(catalog);
            Console.WriteLine("Listing only. No theme was changed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Theme discovery failed: {exception.GetType().Name} (HRESULT 0x{exception.HResult:X8}).");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> ActivateThemeAsync(string requestedThemeId)
    {
        if (string.IsNullOrWhiteSpace(requestedThemeId))
        {
            WriteUsage();
            return 2;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Activation requires an interactive console for confirmation and restoration.");
            return 2;
        }

        try
        {
            var highContrastStateProvider = new WindowsHighContrastStateProvider();
            WindowsThemeCatalog themeCatalog = WindowsThemeCatalogFactory.CreateDefault(
                highContrastStateProvider: highContrastStateProvider);
            var staExecutionContext = new WindowsStaExecutionContext();
            var sessionFactory = new WindowsThemeActivationSessionFactory();

            (IReadOnlyList<ThemeManagerEntry> Entries, ThemeManagerEntry CurrentTheme) snapshot =
                staExecutionContext.Run(() =>
                {
                    using IThemeActivationSession session = sessionFactory.Create();
                    return (
                        Entries: session.GetEntries(),
                        CurrentTheme: session.GetCurrentTheme());
                });

            WindowsThemeCatalogResult catalog = themeCatalog.Discover(
                managerEntries: snapshot.Entries);
            WriteCatalog(catalog);

            if (catalog.HighContrastModeEnabled)
            {
                Console.Error.WriteLine("Activation is disabled while High Contrast is enabled.");
                return 1;
            }

            WindowsThemeDescriptor[] currentThemeMatches = catalog.Themes
                .Where(theme => ThemeCatalogMatcher.Matches(theme, snapshot.CurrentTheme))
                .ToArray();
            if (currentThemeMatches.Length != 1
                || currentThemeMatches[0].MatchStatus != ThemeManagerMatchStatus.Matched)
            {
                Console.Error.WriteLine(
                    "The current theme cannot be mapped to one eligible catalog descriptor. No theme was changed.");
                return 1;
            }

            WindowsThemeDescriptor currentTheme = currentThemeMatches[0];
            WindowsThemeDescriptor? requestedTheme = catalog.Themes.SingleOrDefault(
                theme => string.Equals(theme.Id, requestedThemeId, StringComparison.Ordinal));
            if (requestedTheme is null || requestedTheme.MatchStatus != ThemeManagerMatchStatus.Matched)
            {
                Console.Error.WriteLine(
                    "The requested ID is missing or ineligible. Choose an ID marked [Matched]. No theme was changed.");
                return 1;
            }

            if (string.Equals(currentTheme.Id, requestedTheme.Id, StringComparison.Ordinal))
            {
                Console.Error.WriteLine("The requested theme is already active. Choose a different theme to test activation.");
                return 1;
            }

            Console.WriteLine($"Current theme to restore: {currentTheme.DisplayName} ({currentTheme.Id})");
            Console.WriteLine($"Requested theme: {requestedTheme.DisplayName} ({requestedTheme.Id})");
            Console.WriteLine();
            Console.WriteLine("The probe will apply the requested theme, let you inspect the result, then restore the current theme.");
            Console.WriteLine("Run as a standard user. Do not change themes elsewhere while this probe is running.");
            Console.Write("Type APPLY to continue: ");
            if (!string.Equals(Console.ReadLine(), "APPLY", StringComparison.Ordinal))
            {
                Console.WriteLine("Cancelled. No theme was changed.");
                return 2;
            }

            IWindowsThemeApplier applier = WindowsThemeActivationAdapterFactory.CreateDefault();
            ThemeApplyResult? activationResult = null;
            ThemeApplyResult? restorationResult = null;
            using var inspectionWaitCancellation = new CancellationTokenSource();
            var inspectionWaitCompleted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                inspectionWaitCompleted.TrySetResult(result: true);
                inspectionWaitCancellation.Cancel();
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                try
                {
                    activationResult = await applier.ApplyAsync(requestedTheme);
                    WriteResult("Activation", activationResult);
                    if (activationResult.ApiAccepted
                        && !inspectionWaitCompleted.Task.IsCompleted)
                    {
                        Console.WriteLine(
                            "Check that appearance changed and no Settings or Control Panel window opened. Press Enter or Ctrl+C to restore.");
                        Task<string?> inspectionInputTask = Console.In.ReadLineAsync(
                                inspectionWaitCancellation.Token)
                            .AsTask();
                        Task completedTask = await Task.WhenAny(
                            inspectionInputTask,
                            inspectionWaitCompleted.Task);
                        if (completedTask == inspectionInputTask)
                        {
                            try
                            {
                                _ = await inspectionInputTask;
                            }
                            catch (OperationCanceledException) when (inspectionWaitCancellation.IsCancellationRequested)
                            {
                            }
                        }
                        else
                        {
                            Console.WriteLine("Ctrl+C received. Restoring the previous theme.");
                        }
                    }
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        $"Activation raised {exception.GetType().Name} (HRESULT 0x{exception.HResult:X8}).");
                }
                finally
                {
                    try
                    {
                        restorationResult = await applier.ApplyAsync(
                            requestedTheme: currentTheme,
                            cancellationToken: CancellationToken.None);
                        WriteResult("Restoration", restorationResult);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(
                            $"Restoration raised {exception.GetType().Name} (HRESULT 0x{exception.HResult:X8}).");
                    }
                }

                return activationResult?.Status == ThemeApplyStatus.VerifiedSuccess
                    && restorationResult?.Status == ThemeApplyStatus.VerifiedSuccess
                        ? 0
                        : 1;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"The activation probe failed before the change: {exception.GetType().Name} (HRESULT 0x{exception.HResult:X8}).");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void WriteCatalog(WindowsThemeCatalogResult catalog)
    {
        Console.WriteLine($"High Contrast enabled: {catalog.HighContrastModeEnabled}");
        Console.WriteLine($"Skipped files: {catalog.SkippedFileCount}");
        Console.WriteLine("Catalog themes (use only an ID marked [Matched]):");
        Console.WriteLine();

        foreach (WindowsThemeDescriptor theme in catalog.Themes)
        {
            Console.WriteLine($"[{theme.MatchStatus}] {theme.DisplayName}");
            Console.WriteLine($"    Id: {theme.Id}");
        }

        Console.WriteLine();
    }

    private static void WriteResult(string operation, ThemeApplyResult result)
    {
        Console.WriteLine($"{operation} status: {result.Status}");
        Console.WriteLine($"API accepted: {result.ApiAccepted}");
        Console.WriteLine($"HRESULT: {(result.HResult is int hResult ? $"0x{hResult:X8}" : "(not available)")}");
        Console.WriteLine($"Diagnostic: {result.Diagnostic}");
    }

    private static void WriteUsage()
    {
        Console.WriteLine("Dynamic Dark Mode Windows Theme Activation Probe");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/DynamicDarkMode.ThemeActivationProbe/DynamicDarkMode.ThemeActivationProbe.csproj -- list");
        Console.WriteLine("  dotnet run --project tools/DynamicDarkMode.ThemeActivationProbe/DynamicDarkMode.ThemeActivationProbe.csproj -- activate <theme-file-v1-id>");
        Console.WriteLine();
        Console.WriteLine("The list command is read-only. The activate command requires interactive confirmation and restores the previously active, uniquely matched theme.");
    }
}
