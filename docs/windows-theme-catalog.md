# Windows Theme Catalog

Issue: [#59 — Discover installed Windows themes and resolve stable identities](https://github.com/MarcolinoPT/Dynamic-Dark-Mode-x-Windows/issues/59)

The Windows companion's theme catalog discovers theme package files for the current user and correlates them with the entries exposed by the experimental, undocumented `IThemeManager2` COM interface.

## Discovery roots

The catalog recursively searches for `.theme` files under:

- `%WINDIR%\Resources\Themes` — built-in/system theme packages.
- `%ProgramData%\Microsoft\Windows\Themes` — shared theme packages, when present.
- `%LOCALAPPDATA%\Microsoft\Windows\Themes` — current-user theme packages.

Inaccessible directories and malformed filesystem entries are skipped. Hidden and system entries are included so installed packages are not silently omitted because of filesystem attributes. The result reports how many discovered files could not be parsed. The Ease of Access Themes directory is not a discovery root.

## Metadata and display names

The parser reads the `[Theme]` `DisplayName` value and the `[VisualStyles]` `Path`, `ColorStyle`, and `Size` values.

Indirect display-name resources (for example, `@%SystemRoot%\...`) are resolved with the Windows `SHLoadIndirectString` API. If resolution fails, the descriptor retains the original resource reference and uses the file name as a display fallback. **A fallback name is not considered resolved and is never used to correlate a package with a COM entry.**

Visual-style paths are environment-expanded and normalized before comparison. `%ResourceDir%` resolves to the configured `ResourceDir` environment variable when present, or to the `Resources` directory beneath the Windows installation directory when the variable is absent. The parser and matcher use the same normalization so theme-file and theme-manager paths are compared consistently.

## Application-owned identity

A theme descriptor ID is:

`theme-file-v1:` followed by the SHA-256 hash of the normalized absolute theme-file path (from `Path.GetFullPath`), with case normalized.

This identity is independent of COM enumeration order and does not expose or persist a COM index. It is stable while the package remains at the same path. Moving or reinstalling a package at a different path produces a different ID. It is a location identity, not a content hash.

## Correlation with `IThemeManager2`

The catalog enumerates COM entries on a dedicated STA thread. It correlates a parsed theme file with a COM entry only when all of these match:

- Resolved display name (case-insensitive).
- Normalized `VisualStyle1` path.
- `VisualStyle2` / theme `ColorStyle`.

The catalog does not persist the COM enumeration index. A unique match is represented as `Matched`; no match is `Unmatched`; multiple matching COM entries or multiple files resolving to one COM entry are `Ambiguous`; and a uniquely matched entry disabled by Windows is `Disabled`.

Unmatched, ambiguous, disabled, or unresolved entries must not be silently substituted with another theme. A future activation adapter must re-enumerate and re-resolve the descriptor immediately before applying it.

## High Contrast and accessibility

The catalog reads the current user's High Contrast accessibility state through `SystemParametersInfo(SPI_GETHIGHCONTRAST)`. The result is exposed as `HighContrastModeEnabled`; callers must not initiate theme switching while this flag is true. This is a snapshot: an activation adapter must query the accessibility state again immediately before applying a theme.

High Contrast packages are conservatively excluded when their file is under the Windows Ease of Access Themes directory or their visual style is `AeroLite.msstyles`. This does not depend on English display-name matching. The style/path checks may conservatively exclude a non-High-Contrast theme that deliberately uses AeroLite. Windows does not expose a documented general-purpose API that classifies every arbitrary theme package as a High Contrast package, so this filter is not a universal package classifier.

The catalog does not enable, disable, or modify accessibility settings.

## Scope and limitations

- This is discovery and correlation only; it does not activate themes.
- `IThemeManager2` remains undocumented and may change across Windows builds.
- Theme packages without a resolved display name or a complete VisualStyles tuple remain visible as unmatched descriptors where parsing succeeds, but cannot be correlated safely.
- The current search roots are the supported discovery locations for this slice; themes stored elsewhere are not discovered.
- The current implementation does not parse wallpaper, cursor, sound, or slideshow sections. Those properties are outside this catalog's matching contract.
- The COM enumeration position is intentionally ephemeral and is not part of a descriptor.

## Manual validation

On the target Windows 11 Pro 26H2 VM (build 26300.9818), using a standard non-elevated user:

1. From the repository root, run `dotnet run --project tools/DynamicDarkMode.ThemeCatalogProbe/DynamicDarkMode.ThemeCatalogProbe.csproj -- list`. The probe is diagnostic-only and never activates a theme.
2. Confirm the built-in Windows Light and Dark theme files are discovered and each resolves to one COM entry, regardless of COM enumeration order.
3. Confirm a saved user theme under the current user's theme directory is discovered.
4. Confirm an indirect display name resolves to the localized display string; if resource resolution is unavailable, verify that the original resource reference is retained and the entry remains unmatched.
5. Confirm duplicate display names with distinct style metadata are disambiguated, while duplicate full tuples are marked ambiguous.
6. Confirm the Ease of Access theme directory and AeroLite styles are excluded without inspecting localized display-name text.
7. Enable High Contrast in Windows Accessibility settings and verify `HighContrastModeEnabled` is true; verify the catalog does not change accessibility settings.
8. Restore the user's prior accessibility setting after the test.

Record the exact Windows edition/version/build and actual output in issue #59. Do not treat this documentation or the original spike's results as proof of manual validation for this implementation.
