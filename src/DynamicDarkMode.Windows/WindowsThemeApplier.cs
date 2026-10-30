using DynamicDarkMode.Core;

namespace DynamicDarkMode.Windows;

/// <summary>
/// Applies the standard Light or Dark theme to the current Windows user.
/// </summary>
internal sealed class WindowsThemeApplier : IThemeApplier
{
    private const string SystemUsesLightTheme = "SystemUsesLightTheme";
    private const string AppsUseLightTheme = "AppsUseLightTheme";

    private readonly IWindowsThemeSettingsStore _settingsStore;
    private readonly IWindowsSettingChangeNotifier _notifier;

    public WindowsThemeApplier()
        : this(
            settingsStore: new WindowsThemeSettingsStore(),
            notifier: new WindowsSettingChangeNotifier())
    {
    }

    internal WindowsThemeApplier(
        IWindowsThemeSettingsStore settingsStore,
        IWindowsSettingChangeNotifier notifier)
    {
        _settingsStore = settingsStore;
        _notifier = notifier;
    }

    public Task ApplyAsync(
        ThemeAppearance appearance,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int requestedValue = appearance switch
        {
            ThemeAppearance.Light => 1,
            ThemeAppearance.Dark => 0,
            _ => throw new ArgumentOutOfRangeException(
                paramName: nameof(appearance),
                actualValue: appearance,
                message: "Only Light and Dark appearances can be applied.")
        };

        int? originalSystemValue = _settingsStore.Read(valueName: SystemUsesLightTheme);
        int? originalAppsValue = _settingsStore.Read(valueName: AppsUseLightTheme);

        if (originalSystemValue == requestedValue && originalAppsValue == requestedValue)
        {
            return Task.CompletedTask;
        }

        try
        {
            if (originalSystemValue != requestedValue)
            {
                _settingsStore.Write(valueName: SystemUsesLightTheme, value: requestedValue);
            }

            if (originalAppsValue != requestedValue)
            {
                _settingsStore.Write(valueName: AppsUseLightTheme, value: requestedValue);
            }
        }
        catch (Exception writeException)
        {
            IReadOnlyCollection<Exception> recoveryExceptions = RestoreOriginalSettings(
                originalSystemValue: originalSystemValue,
                originalAppsValue: originalAppsValue);

            throw new ThemeApplicationException(
                message: "Windows rejected the requested theme settings. Restoration of the original values was attempted.",
                operationException: writeException,
                recoveryExceptions: recoveryExceptions);
        }

        try
        {
            _notifier.BroadcastThemeChanged();
        }
        catch (Exception notificationException)
        {
            List<Exception> recoveryExceptions = RestoreOriginalSettings(
                originalSystemValue: originalSystemValue,
                originalAppsValue: originalAppsValue).ToList();

            try
            {
                _notifier.BroadcastThemeChanged();
            }
            catch (Exception rollbackNotificationException)
            {
                recoveryExceptions.Add(rollbackNotificationException);
            }

            throw new ThemeApplicationException(
                message: "Windows theme settings were written, but the change notification failed. Restoration was attempted.",
                operationException: notificationException,
                recoveryExceptions: recoveryExceptions);
        }

        return Task.CompletedTask;
    }

    private IReadOnlyCollection<Exception> RestoreOriginalSettings(
        int? originalSystemValue,
        int? originalAppsValue)
    {
        var exceptions = new List<Exception>();

        try
        {
            _settingsStore.Write(valueName: SystemUsesLightTheme, value: originalSystemValue);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }

        try
        {
            _settingsStore.Write(valueName: AppsUseLightTheme, value: originalAppsValue);
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }

        return exceptions;
    }
}
