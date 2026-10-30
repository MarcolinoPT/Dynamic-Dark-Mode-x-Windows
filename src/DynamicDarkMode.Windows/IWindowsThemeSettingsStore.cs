namespace DynamicDarkMode.Windows;

internal interface IWindowsThemeSettingsStore
{
    int? Read(string valueName);
    void Write(string valueName, int? value);
}
