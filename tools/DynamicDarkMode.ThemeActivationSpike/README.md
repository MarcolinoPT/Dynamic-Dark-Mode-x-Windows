# Windows Theme Activation Spike

This is a standalone, development-only probe for issue [#58](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/58).

It does not form part of the Windows companion and is deliberately not included in the main solution. It experiments with the undocumented `IThemeManager2` COM interface to enumerate installed theme entries, inspect the current theme, and request activation of a selected entry.

## Safety

- Run only in the disposable standard Windows test account.
- Do not run elevated.
- Record the current theme index before applying another theme.
- Do not select High Contrast themes. The warning emitted by the probe is heuristic, not a reliable classifier.
- The apply command requires typing `APPLY` before making a change.
- Restore the previous theme index after testing.
- The COM interface and vtable are undocumented. A crash, incorrect result, or Windows build incompatibility is possible. Do not use this code in the companion.

## Requirements

- Windows 11.
- .NET 10 SDK.
- Standard, non-administrator Windows user.

## Build

From the repository root:

```powershell
dotnet build .\tools\DynamicDarkMode.ThemeActivationSpike\DynamicDarkMode.ThemeActivationSpike.csproj --configuration Release
```

## Commands

List themes exposed by the COM interface:

```powershell
dotnet run --project .\tools\DynamicDarkMode.ThemeActivationSpike\DynamicDarkMode.ThemeActivationSpike.csproj --configuration Release -- list
```

Show the currently active theme:

```powershell
dotnet run --project .\tools\DynamicDarkMode.ThemeActivationSpike\DynamicDarkMode.ThemeActivationSpike.csproj --configuration Release -- current
```

Apply an entry by index (the command prompts for confirmation):

```powershell
dotnet run --project .\tools\DynamicDarkMode.ThemeActivationSpike\DynamicDarkMode.ThemeActivationSpike.csproj --configuration Release -- apply 0
```

The apply command prints the previous index. To restore it, invoke the same command with that index and confirm.

## Manual evidence to record

For each candidate theme, record:

- Windows edition, version and build.
- Theme index, display name and VisualStyle paths.
- Whether Windows reports it as disabled.
- Whether activation returned successfully and the resulting current index.
- Whether any Settings or Control Panel window appeared.
- Whether Settings → Personalization → Themes reflects the selected theme.
- Wallpaper, system colours, app colours, taskbar and Start appearance.
- Any other theme-package properties visibly changed.
- Whether the previously active theme could be restored.
- Any COM exception/HRESULT, process crash, or unexpected behaviour.

Do not treat a successful COM return or changed index alone as proof that all theme-package properties were applied.

## Current limitations

- The interface is undocumented and may vary across Windows releases/builds.
- The current probe displays the metadata exposed by `IThemeManager2`; it does not yet prove that every entry corresponds to an installed, user-selectable theme package.
- High Contrast identification is only a warning heuristic. Do not apply entries that may be High Contrast.
- Theme enumeration order/index stability has not been established. Re-enumerate immediately before applying; never persist an index as a theme identity.
- The probe does not yet resolve localized indirect display-name resources or map theme entries back to their source `.theme` files.
- No production abstraction or integration is introduced.

## Reference

The COM interface identifiers and initial vtable declaration were cross-checked against the MIT-licensed C# reference implementation by namazso, “Setting themes with IThemeManager2 in C#”:
https://gist.github.com/namazso/0fde102c2fc56049c7c37f7fdf9ac3cd

This reference is community research, not Microsoft documentation or a compatibility guarantee.
