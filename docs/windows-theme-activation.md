# Windows Theme Activation

Issue: [#60 — Implement a replaceable Windows theme activation adapter](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/60)

The Windows-specific `IWindowsThemeApplier` boundary accepts an application-owned `WindowsThemeDescriptor` and returns a structured `ThemeApplyResult`. It is separate from the platform-neutral appearance and protocol contracts; this slice adds no UI, configuration, or protocol integration.

## Activation flow

1. The adapter starts a dedicated STA thread and initializes COM with `COINIT_APARTMENTTHREADED`.
2. It creates and initializes one `IThemeManager2` session on that thread.
3. It enumerates the current theme entries through that session and builds a fresh catalog from those entries.
4. It resolves the requested descriptor by its `theme-file-v1:` ID and rejects a missing, unmatched, ambiguous, disabled, unresolved, or otherwise ineligible target.
5. It re-enumerates on the same session, matches the descriptor tuple again, rejects a target that became missing, ambiguous, or disabled, and checks High Contrast immediately before calling `SetCurrentTheme`.
6. It requests activation on the same COM session, then queries the current theme and reports whether Windows reports the requested entry as current.
7. It releases theme and manager COM references and calls `CoUninitialize` on the STA thread.

The transient enumeration index is only a local value taken from the last enumeration immediately before activation. It is not carried by the application-owned descriptor, is never persisted, and is never returned in `ThemeApplyResult`. The adapter has no fallback theme and does not invoke Settings, Control Panel, or a `.theme` file association.

## Results and diagnostics

`ThemeApplyResult` reports the requested application-owned ID, one of three statuses, whether the activation API returned successfully, an optional HRESULT, and a diagnostic:

- `RequestFailed`: target validation, COM setup, enumeration, or the activation request failed. `ApiAccepted` is false.
- `VerificationFailed`: the activation request returned, but Windows reported a different current entry or verification failed. `ApiAccepted` is true.
- `VerifiedSuccess`: the activation request returned and Windows reported the requested current entry. `ApiAccepted` is true.

`VerifiedSuccess` confirms the current-theme index reported by `IThemeManager2`; it does not independently prove that every wallpaper, cursor, sound, or other theme component changed as expected.

## Compatibility and validation

`IThemeManager2` is undocumented and remains isolated behind the replaceable session boundary. The activation method and COM declarations follow the existing #58 spike. That spike demonstrated activation and restoration on Windows 11 Pro 26H2, build 26300.9818, in a standard non-elevated user session; it is feasibility evidence for that environment, not a compatibility guarantee.

Automated tests inject a fake theme-manager session and STA execution context. They cover fresh ID resolution, changing transient indices, missing/disabled/ambiguous targets, High Contrast changes, COM HRESULTs, and post-activation verification results. They do not call Windows COM or change system settings.

The production adapter's silent activation path can be exercised with the separate Windows-only activation probe. First list eligible themes, then use the stable ID for a theme different from the current theme:

```powershell
dotnet run --project tools/DynamicDarkMode.ThemeActivationProbe/DynamicDarkMode.ThemeActivationProbe.csproj -- list
$themeId = 'theme-file-v1:PASTE_ID_FROM_LIST'
dotnet run --project tools/DynamicDarkMode.ThemeActivationProbe/DynamicDarkMode.ThemeActivationProbe.csproj -- activate $themeId
```

The `activate` command captures the current theme only when it maps uniquely to an eligible catalog descriptor, requires the operator to type `APPLY`, invokes the production adapter, pauses for visual inspection, and restores the captured theme before exiting. Press Enter or Ctrl+C at the inspection pause to continue to restoration; Ctrl+C is intercepted so the probe can restore the original theme before exiting. Keep the console open until restoration reports `VerifiedSuccess`. The activation probe is separate from the read-only catalog probe; it does not launch Settings or Control Panel.

Run the activation scenario on the supported Windows VM as a standard user. Confirm Windows reports the requested theme as current, verify the visible appearance changed, confirm no Settings or Control Panel window appeared, and confirm the original theme was restored. Record the Windows edition/version/build, account elevation status, and actual pass/fail results in the issue or PR before treating #60 as fully validated.
