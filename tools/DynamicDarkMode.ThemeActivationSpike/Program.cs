using System.Globalization;
using System.Runtime.InteropServices;

namespace DynamicDarkMode.ThemeActivationSpike;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("This spike can only run on Windows.");
            return 2;
        }

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        int initializationResult = ThemeManagerNativeMethods.CoInitializeEx(
            reserved: IntPtr.Zero,
            coInit: ThemeManagerNativeMethods.CoInitApartmentThreaded);

        if (initializationResult < 0)
        {
            Marshal.ThrowExceptionForHR(initializationResult);
        }

        IThemeManager2? manager = null;

        try
        {
            int creationResult = ThemeManagerNativeMethods.CoCreateInstance(
                classId: ThemeManagerNativeMethods.ThemeManagerClassId,
                outer: IntPtr.Zero,
                classContext: ThemeManagerNativeMethods.ClsctxAll,
                interfaceId: typeof(IThemeManager2).GUID,
                instance: out manager);

            if (creationResult < 0)
            {
                Marshal.ThrowExceptionForHR(creationResult);
            }

            if (manager is null)
            {
                throw new InvalidOperationException("Windows returned no IThemeManager2 instance.");
            }

            manager.Init(ThemeManagerInitializationFlags.None);

            return args[0].ToLowerInvariant() switch
            {
                "list" when args.Length == 1 => ListThemes(manager),
                "current" when args.Length == 1 => ShowCurrentTheme(manager),
                "apply" when args.Length == 2 => ApplyTheme(manager, args[1]),
                _ => InvalidArguments()
            };
        }
        catch (COMException exception)
        {
            Console.Error.WriteLine($"Windows theme COM operation failed: 0x{exception.HResult:X8}");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Theme operation failed: {exception.GetType().Name}");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        finally
        {
            if (manager is not null && Marshal.IsComObject(manager))
            {
                Marshal.ReleaseComObject(manager);
            }

            ThemeManagerNativeMethods.CoUninitialize();
        }
    }

    private static int ListThemes(IThemeManager2 manager)
    {
        manager.GetThemeCount(out int count);
        manager.GetCurrentTheme(out int currentIndex);

        Console.WriteLine($"Installed theme entries: {count}");
        Console.WriteLine($"Current theme index: {currentIndex}");
        Console.WriteLine();

        for (int index = 0; index < count; index++)
        {
            try
            {
                manager.GetTheme(index, out IWindowsTheme theme);

                try
                {
                    manager.IsThemeDisabled(index, out int disabled);
                    string displayName = ReadProperty(() => theme.DisplayName);
                    string visualStyle1 = ReadProperty(() => theme.VisualStyle1);
                    string visualStyle2 = ReadProperty(() => theme.VisualStyle2);
                    bool isCurrent = index == currentIndex;
                    bool mayBeHighContrast = LooksLikeHighContrastTheme(
                        displayName,
                        visualStyle1,
                        visualStyle2);

                    Console.WriteLine($"[{index}] {displayName}");
                    Console.WriteLine($"    Current: {isCurrent}");
                    Console.WriteLine($"    Disabled by Windows: {disabled != 0}");
                    Console.WriteLine($"    VisualStyle1: {visualStyle1}");
                    Console.WriteLine($"    VisualStyle2: {visualStyle2}");

                    if (mayBeHighContrast)
                    {
                        Console.WriteLine("    WARNING: This entry may be a High Contrast theme. Do not apply it.");
                    }

                    Console.WriteLine();
                }
                finally
                {
                    ReleaseComObject(theme);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[{index}] Enumeration failed: {exception.Message}");
                Console.WriteLine();
            }
        }

        Console.WriteLine("Theme metadata is exploratory. Verify candidate themes in Windows Settings before applying.");
        Console.WriteLine("High Contrast detection is heuristic and is not a production safety guarantee.");
        return 0;
    }

    private static int ShowCurrentTheme(IThemeManager2 manager)
    {
        manager.GetCurrentTheme(out int currentIndex);
        manager.GetTheme(currentIndex, out IWindowsTheme theme);

        try
        {
            Console.WriteLine($"Current theme index: {currentIndex}");
            Console.WriteLine($"Display name: {ReadProperty(() => theme.DisplayName)}");
            Console.WriteLine($"VisualStyle1: {ReadProperty(() => theme.VisualStyle1)}");
            Console.WriteLine($"VisualStyle2: {ReadProperty(() => theme.VisualStyle2)}");
            return 0;
        }
        finally
        {
            ReleaseComObject(theme);
        }
    }

    private static int ApplyTheme(IThemeManager2 manager, string indexText)
    {
        if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out int requestedIndex))
        {
            Console.Error.WriteLine("Theme index must be a non-negative integer.");
            return 2;
        }

        manager.GetThemeCount(out int count);
        if (requestedIndex < 0 || requestedIndex >= count)
        {
            Console.Error.WriteLine($"Theme index must be between 0 and {count - 1}.");
            return 2;
        }

        manager.IsThemeDisabled(requestedIndex, out int disabled);
        if (disabled != 0)
        {
            Console.Error.WriteLine("Windows marks this theme as disabled; refusing to apply it.");
            return 1;
        }

        manager.GetCurrentTheme(out int previousIndex);
        manager.GetTheme(requestedIndex, out IWindowsTheme selectedTheme);

        string displayName;
        try
        {
            displayName = ReadProperty(() => selectedTheme.DisplayName);
        }
        finally
        {
            ReleaseComObject(selectedTheme);
        }

        Console.WriteLine($"Current theme index: {previousIndex}");
        Console.WriteLine($"Requested theme: [{requestedIndex}] {displayName}");
        Console.WriteLine();
        Console.WriteLine("This changes the current Windows user's active theme.");
        Console.WriteLine("Do not select a High Contrast theme. A heuristic warning is shown by the list command.");
        Console.Write("Type APPLY to continue: ");

        if (!string.Equals(Console.ReadLine(), "APPLY", StringComparison.Ordinal))
        {
            Console.WriteLine("Cancelled. No theme change was requested.");
            return 0;
        }

        manager.SetCurrentTheme(
            parentWindow: IntPtr.Zero,
            themeIndex: requestedIndex,
            applyNow: 1,
            applyFlags: ThemeApplyFlags.None,
            packFlags: ThemePackFlags.None);

        manager.GetCurrentTheme(out int resultingIndex);
        if (resultingIndex != requestedIndex)
        {
            Console.Error.WriteLine(
                $"The COM call returned, but Windows reports current theme index {resultingIndex}, not {requestedIndex}.");
            return 1;
        }

        Console.WriteLine("Windows reports the requested theme as active.");
        Console.WriteLine($"Previous theme index: {previousIndex}");
        Console.WriteLine($"To restore it, run: dotnet run --project .\\tools\\DynamicDarkMode.ThemeActivationSpike\\DynamicDarkMode.ThemeActivationSpike.csproj --configuration Release -- apply {previousIndex}");
        Console.WriteLine("Confirm the result in Settings → Personalization → Themes and inspect wallpaper and appearance manually.");
        return 0;
    }

    private static bool LooksLikeHighContrastTheme(
        string displayName,
        string visualStyle1,
        string visualStyle2)
    {
        string combined = $"{displayName} {visualStyle1} {visualStyle2}";
        return combined.Contains("high contrast", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("ease of access", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadProperty(Func<string> read)
    {
        try
        {
            return read() ?? "(not available)";
        }
        catch (Exception exception)
        {
            return $"(unavailable: {exception.Message})";
        }
    }

    private static void ReleaseComObject(object value)
    {
        if (Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private static int InvalidArguments()
    {
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Dynamic Dark Mode Windows Theme Activation Spike");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  list       Enumerate themes exposed by IThemeManager2.");
        Console.WriteLine("  current    Show the current theme index and metadata.");
        Console.WriteLine("  apply N    Interactively apply theme index N to the current user.");
        Console.WriteLine();
        Console.WriteLine("Run from a standard, non-elevated Windows user session.");
        Console.WriteLine("This uses an undocumented Windows COM interface and is experimental.");
    }
}
