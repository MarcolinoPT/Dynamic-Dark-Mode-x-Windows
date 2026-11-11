# Decision Note — Silent Windows Theme Activation

Issue: [#58 — Investigate silent activation of complete installed Windows themes](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/58)

Branch: `spike/windows-theme-activation-58`

Date: 2026-11-11

## Executive summary

**Recommendation: retain `IThemeManager2` as the leading implementation candidate, behind a narrow Windows-specific adapter, but do not treat it as a supported Windows API or ship the current spike code as production code.**

The standalone probe successfully enumerated the target user's theme entries, activated the built-in Windows Light and Dark themes without an observed Settings/Control Panel launch, and restored a saved theme. The tester confirmed the visible appearance matched the selected Light and Dark themes.

This is strong feasibility evidence for the tested environment:

- Windows 11 Pro 26H2, build 26300.9818
- Standard, non-elevated user session
- .NET 10
- Parallels Windows VM

It is not yet evidence of compatibility across Windows builds, reliable discovery of arbitrary user-installed themes, stable theme identity, or safe behaviour for every theme package.

## Evidence collected

### Discovery

The probe returned 12 entries. The target installation exposed:

- `Windows (light)` at index 5
- `Windows (dark)` at index 6
- A saved user theme, `Spike backup`, at index 0
- Four clearly named High Contrast entries at indices 1–4
- Other built-in themes at indices 7–11

The current theme query correctly reported the selected entry before and after each test.

The indices are observed enumeration positions, **not stable identifiers**. They must never be persisted or treated as durable IDs. Enumeration order may change with Windows version, installed themes, user profile state, or other environmental factors.

The probe's High Contrast check is only a heuristic based on English display-name/path strings. It is not suitable as a production exclusion mechanism.

### Activation

| Test | Result | Evidence |
|---|---|---|
| Windows (light) | Pass | User visually confirmed the resulting theme; current-theme query returned index 5 and `Windows (light)`. |
| Windows (dark) | Pass | User confirmed the dark theme was active; current-theme query returned index 6 and `Windows (dark)`. |
| Restore saved theme | Pass | User confirmed `Spike backup` was active again at index 0. |

The tests were performed interactively using the probe's explicit confirmation prompt. The tester reported that the resulting appearance matched the selected themes. The tests demonstrate activation of the built-in Light/Dark entries and restoration of a saved user theme on this build.

The probe's `SetCurrentTheme` call uses `applyNow: 1` and no ignore flags. The successful visual tests indicate that Windows applied the expected observable appearance for these built-in entries. The spike did not independently inspect every underlying theme component or verify all package types.

### Threading and COM behaviour

The probe:

- Initializes COM with `COINIT_APARTMENTTHREADED`.
- Creates the COM object using the observed CLSID and IID.
- Calls `Init` before using the manager.
- Releases acquired COM references and uninitializes COM.
- Reports COM HRESULTs via exceptions where surfaced by COM interop.

This worked in the tested process and environment. It does not prove that the interface's threading, marshaling, or vtable contract is stable.

## Mechanism assessment

### `IThemeManager2`

**Advantages**

- Supports enumeration and current-theme inspection in the tested environment.
- Successfully activated the built-in Light and Dark entries.
- Supports a direct activation request without the observed Settings UI launch.
- A saved user theme could be restored using its enumerated entry.
- Community projects, including Auto Dark Mode, also use this interface for theme switching.

**Risks**

- The interface, CLSID/IID, vtable layout, flags, and semantics are undocumented and not a Microsoft-supported application contract.
- The current implementation relies on a community-researched COM declaration. A Windows update could change behaviour or break compatibility.
- An index is only an ephemeral position in the current enumeration.
- Display names can be localized or represented indirectly; string matching is not a robust identity mechanism.
- The probe has only tested built-in Light/Dark activation and one saved theme restoration. It has not validated arbitrary downloaded/user-created theme packages.
- The current High Contrast guard is not safe enough for production.
- A successful COM return/current index does not, by itself, prove all theme components were applied.
- Failure and partial-application behaviour have not been systematically exercised.

### Documented `.theme` mechanism

Microsoft documents the theme file format and explains that invoking a `.theme` file through its file association opens the Personalization UI. This is useful for parsing and understanding theme packages, but does not provide the required documented silent activation mechanism.

Reference: [Microsoft Learn — Theme File Format](https://learn.microsoft.com/en-us/windows/win32/controls/themesfileformat-overview).

No documented alternative has been identified in this spike that provides equivalent silent, complete theme activation for an interactive user's session.

## Production direction

If the product requirement remains silent activation of complete installed themes, proceed with `IThemeManager2` as a **replaceable implementation detail**, subject to further validation.

Introduce a narrow abstraction, for example:

- `IWindowsThemeCatalog`: discovers themes and returns application-owned descriptors.
- `IWindowsThemeApplier`: applies a descriptor and reports an explicit result.
- A Windows implementation backed by `IThemeManager2`.

Do not expose COM indices outside the implementation. A descriptor should carry an application-owned identity and enough source metadata to re-resolve the target immediately before applying it.

Discovery should be a separate concern from activation. Investigate enumerating the relevant system and per-user `.theme` files, reading their metadata with Windows-compatible INI/resource APIs, resolving indirect display-name resources, and matching those files to the entries exposed by `IThemeManager2`. Handle duplicate names, missing files, localized names, and entries that cannot be confidently matched. Do not silently select a different theme when resolution is ambiguous.

For High Contrast exclusion, use explicit accessibility-state detection and conservative candidate validation rather than English display-name substring matching. If a candidate cannot be classified safely, reject it or require an explicit policy decision; do not assume it is safe.

The adapter should:

1. Run COM work on a dedicated STA execution context.
2. Initialize and release COM deterministically.
3. Re-enumerate immediately before resolving/applying a theme.
4. Validate the selected descriptor against the current catalog.
5. Never persist or expose raw enumeration indices as IDs.
6. Return structured success/failure information, including HRESULT where available.
7. Verify the resulting current theme after application.
8. Distinguish API acceptance from verified application.
9. Avoid automatic fallback to another theme.
10. Log Windows version/build and diagnostic details without leaking user-specific theme paths unnecessarily.

## Remaining validation before production use

1. Test a custom/user-created `.theme` package with a distinctive wallpaper and, if practical, sounds/cursors.
2. Test a downloaded theme package installed for the current user.
3. Verify how entries map to source `.theme` files, including indirect/localized names and duplicate display names.
4. Reboot and re-enumerate to check index/order changes and persistence.
5. Test unavailable, disabled, deleted, and malformed theme entries.
6. Exercise COM failures and confirm the app does not report false success.
7. Test the supported Windows versions/builds that the product intends to claim.
8. Verify High Contrast is excluded using accessibility state, not name heuristics.
9. Confirm the production caller's threading model and session context are compatible with the COM component.

## Decision

**Proceed to a small, separate production-design/implementation task for a replaceable `IThemeManager2` adapter, but keep the above validation gates explicit.**

Do not merge the spike into the production companion. Keep it isolated as experimental evidence. The current result is sufficient to justify the next vertical slice, not to claim a fully supported or universally reliable Windows theme API.
