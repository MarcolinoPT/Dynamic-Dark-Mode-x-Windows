# Windows theme application manual testing

The development-only harness invokes `WindowsThemeApplier`, the theme application implementation intended for the Windows companion. The companion does not yet call it. The harness runs in the context of the currently signed-in user and does not require the receiver, network listener, pairing, or macOS app.

## Build

From the repository root, on Windows with the .NET 10 SDK:

```powershell
dotnet build .\tools\DynamicDarkMode.ThemeTestHarness\DynamicDarkMode.ThemeTestHarness.csproj --configuration Release
```

## Run

Run the harness from a normal, non-elevated terminal in the disposable test user's session:

```powershell
dotnet run --project .\tools\DynamicDarkMode.ThemeTestHarness\DynamicDarkMode.ThemeTestHarness.csproj --configuration Release -- light
dotnet run --project .\tools\DynamicDarkMode.ThemeTestHarness\DynamicDarkMode.ThemeTestHarness.csproj --configuration Release -- dark
```

The harness accepts exactly one argument: `light` or `dark`. Exit code `0` indicates success; a non-zero exit code indicates failure. A failure includes the original operation error and any errors encountered while attempting to restore the previous settings.

## Manual acceptance environment

Use a disposable standard (non-administrator) Windows user in the Parallels VM. Keep the normal Windows account's theme as an untouched control.

1. Record the Windows edition, version and build.
2. Record the normal account's current theme.
3. Sign in as the disposable standard user.
4. Set that user's starting theme to Dark in Windows Settings.
5. Run the Light command and verify Windows adopts Light appearance.
6. Run the Dark command and verify Windows adopts Dark appearance.
7. Sign out and return to the normal account; verify its theme is unchanged.
8. Record the commands, actual results, pass/fail outcome and redacted evidence.

Do not run the harness elevated. The implementation writes only to the current user's `HKCU` theme settings.
