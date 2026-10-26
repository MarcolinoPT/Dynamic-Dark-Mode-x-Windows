# Development baseline

**Status:** Initial source inspection  
**Inspected:** 2026-10-26  
**Purpose:** Record the existing application's structure and current development constraints before adding Windows theme synchronisation.

## Repository and build setup

The repository contains the Xcode project `Dynamic Dark Mode.xcodeproj` and workspace `Dynamic Dark Mode.xcworkspace`. The project defines three native targets:

- `Dynamic Dark Mode` — main macOS application
- `DynamicLauncher` — login/launcher helper
- `Tools` — command-line/tooling target

The project settings inspected declare Swift language mode 5. Most target configurations declare a macOS deployment target of 11.0; project-level configurations include 11.5. The `Podfile` declares macOS 10.14, but that is not sufficient to establish the app's actual supported OS range. Effective settings should be confirmed through Xcode before changing deployment targets.

### Dependencies

Swift Package Manager references:

- XMLCoder — `https://github.com/MaxDesiatov/XMLCoder.git`
- Sparkle — `https://github.com/sparkle-project/Sparkle`
- MASShortcut — `https://github.com/shpakovski/MASShortcut`
- Schedule — `https://github.com/luoxiu/Schedule`

The project also uses CocoaPods. `Podfile` declares `LetsMove`; `Podfile.lock` records LetsMove 1.25 and CocoaPods 1.16.2. The Swift package references and project file were inspected, but the exact resolved versions of all Swift packages have not yet been recorded in this baseline.

### Local toolchain observed

On the inspected development machine:

- Apple Command Line Tools are installed at `/Library/Developer/CommandLineTools`.
- Swift reports version 6.4, targeting `arm64-apple-macosx26.0`.
- `xcodebuild` is present, but cannot run because the active developer directory is Command Line Tools rather than a full Xcode installation.
- The `pod` executable is not installed.
- The working tree was clean during inspection.

As a result, a full Xcode project listing/build and an existing-app test run have not been performed. This is a validation limitation, not evidence that the project fails to build.

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

A source-tree scan found no Swift test files or obvious Xcode test targets in the inspected project file. The project file does set `ENABLE_TESTABILITY = YES` in a configuration, but that alone does not mean a test suite exists.

Before implementation, establish an explicit test strategy for the new protocol and Windows companion. Prefer automated tests for protocol parsing, message validation, reconnection behaviour and state-change handling, supplemented by manual end-to-end checks in the macOS host and Windows 11 VM.

## Implications for the Windows integration

1. Reuse the existing effective-appearance observation as a candidate source of macOS appearance state, but keep theme synchronisation separate from wallpaper and scheduling behaviour.
2. Do not reuse `NWPathMonitor` as if it were a VM connection. VM networking and connection direction still require a focused investigation.
3. Preserve existing scheduling, brightness, shortcut, Touch Bar and launcher behaviour unless a separately reviewed change requires otherwise.
4. Choose and document the protocol and local security model before coupling the macOS app to a Windows implementation.
5. Confirm the baseline build with full Xcode and resolve dependencies before treating the current application as build-verified.

## Inspection scope and caveats

This document records a source-level inspection of the listed files and project settings. It is not a successful build report, a complete dependency audit, or a guarantee of compatibility with every macOS/Windows configuration. Revisit it after the first full Xcode build and update it when verified facts change.
