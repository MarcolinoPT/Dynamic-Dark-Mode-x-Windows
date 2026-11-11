using DynamicDarkMode.Windows.ThemeCatalog;

namespace DynamicDarkMode.ThemeCatalogProbe;

internal static class Program
{
    /// <summary>Runs the read-only theme catalog diagnostic command.</summary>
    /// <param name="args">Command-line arguments; the supported command is <c>list</c>.</param>
    /// <returns>Zero on success, or a nonzero exit code when the command cannot run.</returns>
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The theme catalog probe can only run on Windows.");
            return 2;
        }

        if (args.Length != 1 || !string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Dynamic Dark Mode Windows Theme Catalog Probe");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run --project tools/DynamicDarkMode.ThemeCatalogProbe/DynamicDarkMode.ThemeCatalogProbe.csproj -- list");
            Console.WriteLine();
            Console.WriteLine("This probe only discovers and reports themes. It does not activate or modify themes.");
            return args.Length == 0 ? 0 : 2;
        }

        try
        {
            WindowsThemeCatalogResult result = WindowsThemeCatalogFactory.CreateDefault().Discover();

            Console.WriteLine($"High Contrast mode enabled: {result.HighContrastModeEnabled}");
            Console.WriteLine($"Theme files skipped during parsing: {result.SkippedFileCount}");
            Console.WriteLine($"Theme descriptors: {result.Themes.Count}");
            Console.WriteLine();

            foreach (WindowsThemeDescriptor theme in result.Themes)
            {
                Console.WriteLine($"[{theme.MatchStatus}] {theme.DisplayName}");
                Console.WriteLine($"    Id: {theme.Id}");
                Console.WriteLine($"    Built-in: {theme.IsBuiltIn}");
                Console.WriteLine($"    Display name resolved: {theme.IsDisplayNameResolved}");
                Console.WriteLine($"    Theme file: {theme.FilePath}");
                Console.WriteLine($"    Visual style: {theme.VisualStylePath ?? "(not available)"}");
                Console.WriteLine($"    Color style: {theme.ColorStyle ?? "(not available)"}");
                Console.WriteLine($"    Size: {theme.Size ?? "(not available)"}");

                if (theme.UnresolvedDisplayNameResource is not null)
                {
                    Console.WriteLine(
                        $"    Unresolved display-name resource: {theme.UnresolvedDisplayNameResource}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Discovery only. No theme was changed.");
            Console.WriteLine("Do not treat COM enumeration indices as stable theme identities.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Theme discovery failed: {exception.GetType().Name}");
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
