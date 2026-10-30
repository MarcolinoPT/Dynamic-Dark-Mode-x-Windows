using DynamicDarkMode.Core;
using DynamicDarkMode.Windows;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: DynamicDarkMode.ThemeTestHarness.exe <light|dark>");
    return 2;
}

ThemeAppearance appearance = args[0].ToLowerInvariant() switch
{
    "light" => ThemeAppearance.Light,
    "dark" => ThemeAppearance.Dark,
    _ => ThemeAppearance.Unknown
};

if (appearance == ThemeAppearance.Unknown)
{
    Console.Error.WriteLine("Appearance must be either 'light' or 'dark'.");
    return 2;
}

try
{
    await new WindowsThemeApplier().ApplyAsync(appearance: appearance);
    Console.WriteLine($"Successfully applied the {appearance} Windows theme for the current user.");
    return 0;
}
catch (ThemeApplicationException exception)
{
    Console.Error.WriteLine($"Failed to apply the {appearance} Windows theme.");
    Console.Error.WriteLine(exception.Message);
    Console.Error.WriteLine($"Operation error: {exception.OperationException.Message}");

    foreach (Exception recoveryException in exception.RecoveryExceptions)
    {
        Console.Error.WriteLine($"Recovery error: {recoveryException.Message}");
    }

    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Failed to apply the {appearance} Windows theme: {exception.Message}");
    return 1;
}
