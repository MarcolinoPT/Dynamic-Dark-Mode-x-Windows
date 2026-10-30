using System.Security.Principal;
using DynamicDarkMode.Core;

namespace DynamicDarkMode.Windows;

/// <summary>
/// Applies the standard Light or Dark theme to the current Windows user.
/// </summary>
internal sealed class WindowsThemeApplier : IThemeApplier
{
    private const string SystemUsesLightTheme = "SystemUsesLightTheme";
    private const string AppsUseLightTheme = "AppsUseLightTheme";
    private const string MutexNamePrefix = @"Local\DynamicDarkMode.ThemeApplication.";

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

        string userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("The current Windows user's SID could not be determined.");

        using var mutex = new Mutex(
            initiallyOwned: false,
            name: MutexNamePrefix + userSid);

        bool ownsMutex = false;
        try
        {
            try
            {
                mutex.WaitOne();
                ownsMutex = true;
            }
            catch (AbandonedMutexException)
            {
                // The previous owner terminated unexpectedly. The mutex is now
                // owned by this thread, so inspect the settings and proceed.
                ownsMutex = true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            ApplyUnderMutex(requestedValue);
            return Task.CompletedTask;
        }
        finally
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private void ApplyUnderMutex(int requestedValue)
    {
        int? originalSystemValue = _settingsStore.Read(valueName: SystemUsesLightTheme);
        int? originalAppsValue = _settingsStore.Read(valueName: AppsUseLightTheme);

        if (originalSystemValue == requestedValue && originalAppsValue == requestedValue)
        {
            return;
        }

        bool systemWriteAttempted = false;
        bool appsWriteAttempted = false;

        try
        {
            if (originalSystemValue != requestedValue)
            {
                systemWriteAttempted = true;
                _settingsStore.Write(valueName: SystemUsesLightTheme, value: requestedValue);
            }

            if (originalAppsValue != requestedValue)
            {
                appsWriteAttempted = true;
                _settingsStore.Write(valueName: AppsUseLightTheme, value: requestedValue);
            }
        }
        catch (Exception writeException)
        {
            IReadOnlyCollection<Exception> recoveryExceptions = RestoreOriginalSettings(
                originalSystemValue,
                originalAppsValue,
                requestedValue,
                systemWriteAttempted,
                appsWriteAttempted);

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
                originalSystemValue,
                originalAppsValue,
                requestedValue,
                systemWriteAttempted,
                appsWriteAttempted).ToList();

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
    }

    private IReadOnlyCollection<Exception> RestoreOriginalSettings(
        int? originalSystemValue,
        int? originalAppsValue,
        int requestedValue,
        bool systemWriteAttempted,
        bool appsWriteAttempted)
    {
        var exceptions = new List<Exception>();

        RestoreOriginalSetting(
            SystemUsesLightTheme,
            originalSystemValue,
            requestedValue,
            systemWriteAttempted,
            exceptions);

        RestoreOriginalSetting(
            AppsUseLightTheme,
            originalAppsValue,
            requestedValue,
            appsWriteAttempted,
            exceptions);

        return exceptions;
    }

    private void RestoreOriginalSetting(
        string valueName,
        int? originalValue,
        int requestedValue,
        bool writeAttempted,
        ICollection<Exception> exceptions)
    {
        if (!writeAttempted)
        {
            return;
        }

        try
        {
            // Do not overwrite a value that no longer matches what this
            // operation attempted to apply.
            if (_settingsStore.Read(valueName) == requestedValue)
            {
                _settingsStore.Write(valueName, originalValue);
            }
        }
        catch (Exception exception)
        {
            exceptions.Add(exception);
        }
    }
}
