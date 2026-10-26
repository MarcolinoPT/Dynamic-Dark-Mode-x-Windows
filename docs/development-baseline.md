# Development baseline

**Status:** Initial source inspection and local baseline validation  
**Inspected:** 2026-10-26  
**Purpose:** Record the existing application's structure and development constraints before adding Windows theme synchronisation.

## Repository and build setup

The repository contains the Xcode project `Dynamic Dark Mode.xcodeproj` and workspace `Dynamic Dark Mode.xcworkspace`. The project defines three native targets/schemes:

- `Dynamic Dark Mode` — main macOS application
- `DynamicLauncher` — login/launcher helper
- `Tools` — command-line/tooling target

The project uses Swift language mode 5. Most app target configurations declare macOS 11.0; project-level configurations include 11.5. The `Podfile` declares macOS 10.14, but that does not establish the app's actual supported OS range.

### Dependencies

Swift Package Manager resolved these versions during the local build:

- XMLCoder — 0.17.1
- Sparkle — 2.7.0
- MASShortcut — branch `master`, commit `6f2603c`
- Schedule — 2.1.1

The project also uses CocoaPods. `Podfile` declares `LetsMove`; `Podfile.lock` records LetsMove 1.25 and originally records CocoaPods 1.16.2. On this machine, CocoaPods 1.17.0 successfully installed LetsMove 1.25. Its only tracked lockfile difference was the `COCOAPODS` metadata version changing from 1.16.2 to 1.17.0; the Podfile checksum and dependency version were unchanged.

### Local toolchain observed

On the inspected Apple Silicon development machine:

- Full Xcode 27.1 is installed; build version 27A9275.
- The build uses the macOS 27.0 SDK and Swift 6.4 in Swift language mode 5.
- CocoaPods 1.17.0 successfully generated the Pods project and integrated LetsMove 1.25.
- The project’s configured deployment targets are older than the range supported by this Xcode SDK: app and launcher targets use macOS 11.0, the Pods aggregate target uses 10.14, and LetsMove uses 10.6. A normal build reports that this toolchain supports deployment targets from macOS 12.0 to 27.0.
- The project also references a Mac Development signing identity that is not installed on this machine.

These are inherited build-configuration constraints. The successful diagnostic build used command-line overrides; no project deployment-target or signing settings were changed.

## Reproducible local diagnostic build

From the repository root, install CocoaPods dependencies:

```sh
pod install
```

Then build the existing app on Apple Silicon:

```sh
xcodebuild \
  -workspace "Dynamic Dark Mode.xcworkspace" \
  -scheme "Dynamic Dark Mode" \
  -configuration Debug \
  -destination 'platform=macOS,arch=arm64' \
  MACOSX_DEPLOYMENT_TARGET=12.0 \
  CODE_SIGNING_ALLOWED=NO \
  build
```

**Observed result:** build succeeded. The command-line deployment-target override is a diagnostic workaround for this Xcode toolchain, not a decision to drop support for macOS 11. Code signing is disabled only for this local build invocation.

## Existing application behaviour

### Appearance observation

`Dynamic/Components/AppleInterfaceStyle+Coordinator.swift` defines `AppleInterfaceStyleCoordinator`. Its `setup()` method observes `NSApp.effectiveAppearance` through Key-Value Observing. The current observer calls `AppleInterfaceStyle.updateWallpaper()`; it does not send appearance state to another machine.

The coordinator also manages the existing scheduler and screen-brightness observer. Its teardown cancels the scheduler, stops connectivity and brightness observation, and normally invalidates the appearance observation.

### Existing connectivity handling

`Dynamic/Auxiliary/Connectivity.swift` uses `NWPathMonitor` to observe general network-path changes. On a later satisfied path update, it ignores expensive paths and, on supported macOS versions, constrained paths, then schedules the existing scheduler after five seconds.

This is existing scheduling behaviour. It does **not** establish a connection to a Windows VM, prove that host-to-guest networking works, or provide a theme-sync transport.

### Preferences and startup

`Dynamic/Components/Preferences.swift` stores settings through `UserDefaults` and observes several preferences to reconfigure scheduling, brightness observation, shortcuts, Touch Bar behaviour and login launch. When the system's automatic appearance mode is configured, it calls the private `SLSGetAppearanceThemeSwitchesAutomatically` and `SLSSetAppearanceThemeSwitchesAutomatically` APIs.

`Dynamic/View Controller/AppDelegate.swift` sets up notifications, Touch Bar and shortcuts at launch. If the app has launched before, it starts preference observation and configures the appearance coordinator; otherwise, it shows the welcome flow. On termination it tears down the relevant observers and UI components.

`DynamicLauncher/AppDelegate.swift` checks whether the main app is active, opens the app by bundle identifier when needed, and then terminates the launcher.

## Tests and validation

A source-tree scan found no Swift test files or obvious Xcode test targets in the inspected project file. The project file sets `ENABLE_TESTABILITY = YES` in a configuration, but that alone does not mean a test suite exists.

Manual baseline checks on the M4 succeeded:

- The built app launched normally.
- The menu bar app/preferences were accessible.
- Light/Dark theme switching worked.
- Manual appearance overrides remained usable.
- Scheduling controls worked as expected.
- Quitting and relaunching preserved normal behaviour/preferences.
- The existing `DynamicLauncher` build and launch-helper behaviour worked.

These are manual checks, not automated regression tests, and do not establish compatibility with every macOS version or configuration.

Before implementation, establish an explicit test strategy for the new protocol and Windows companion. Prefer automated tests for protocol parsing, message validation, reconnection behaviour and state-change handling, supplemented by manual end-to-end checks in the macOS host and Windows 11 VM.

## Implications for the Windows integration

1. Reuse the existing effective-appearance observation as a candidate source of macOS appearance state, but keep theme synchronisation separate from wallpaper and scheduling behaviour.
2. Do not reuse `NWPathMonitor` as if it were a VM connection. VM networking and connection direction still require a focused investigation.
3. Preserve existing scheduling, brightness, shortcut, Touch Bar and launcher behaviour unless a separately reviewed change requires otherwise.
4. Choose and document the protocol and local security model before coupling the macOS app to a Windows implementation.
5. Resolve the legacy deployment-target and signing configuration deliberately; do not treat the diagnostic build override as the project's permanent support policy.

## Inspection scope and caveats

This document records source-level inspection and a successful local diagnostic build/manual smoke test. It is not a complete dependency audit, automated test report, or guarantee of compatibility with every macOS/Windows configuration. Revisit it when verified facts or build settings change.
