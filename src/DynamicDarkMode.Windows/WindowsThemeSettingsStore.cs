using Microsoft.Win32;

namespace DynamicDarkMode.Windows;

internal sealed class WindowsThemeSettingsStore : IWindowsThemeSettingsStore
{
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public int? Read(string valueName)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            name: PersonalizeKeyPath,
            writable: false);

        if (key is null || key.GetValue(name: valueName) is null)
        {
            return null;
        }

        if (key.GetValueKind(name: valueName) != RegistryValueKind.DWord)
        {
            throw new InvalidOperationException(
                $"The current user's {valueName} theme setting is not a DWORD value.");
        }

        return (int)key.GetValue(name: valueName)!;
    }

    public void Write(string valueName, int? value)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            subkey: PersonalizeKeyPath,
            writable: true)
            ?? throw new InvalidOperationException("The current user's theme settings key could not be opened.");

        if (value.HasValue)
        {
            key.SetValue(
                name: valueName,
                value: value.Value,
                valueKind: RegistryValueKind.DWord);
        }
        else
        {
            key.DeleteValue(name: valueName, throwOnMissingValue: false);
        }
    }
}
