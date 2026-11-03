# QuickAction

A lightweight shortcut dock for Windows 11 x64, built with WPF and .NET 10. QuickAction supports English and Vietnamese and does not need to run as administrator.

[Project website](https://evilartist.github.io/QuickAction/) · [Privacy Policy](https://evilartist.github.io/QuickAction/privacy.html)

## Getting started

Extract `dist/QuickAction-win-x64.zip` into a permanent folder and open `QuickAction.exe`. Hover over the white bar at the left edge of your screen to expand the dock. Use ⚙ to open Settings and ▦ to show all apps. Add shortcuts in Settings or drop EXE, BAT, CMD, PS1, or MSI files onto the dock.

Settings opens automatically when there are no shortcuts. See [VALIDATION.md](VALIDATION.md) for completed checks and scenarios that still need testing on the target machine.

### Language and dock settings

Go to **Settings → Dock & system → Language / Ngôn ngữ** and choose **English** or **Tiếng Việt**. In Vietnamese, the section is named **Dock & hệ thống**. Changes in this section take effect and save automatically, without restarting the app. The collapse delay accepts values from 200 to 5000 ms.

Your language preference is included in configuration backups. Existing configurations without a language preference default to Vietnamese.

### Managing shortcuts

- Drag the idle bar or an empty part of the dock to change its monitor or screen edge. Drag one pinned icon onto another to reorder them.
- Each shortcut has its own name, program, arguments, working directory, and administrator setting. You can add the same executable multiple times with different arguments.
- **Settings → Choose installed app…** opens a searchable grid of installed apps, including Microsoft Store apps. Desktop apps use their executable path and launch arguments. Store apps and entries without a direct executable use a `shell:AppsFolder\…` identifier, which does not support custom arguments or administrator permissions. The app icon is saved with the shortcut. Choose an app, then select **Save shortcut**. Portable apps not registered with Windows can be added by file path.
- Choose a built-in icon, use the program's icon, or select a custom image. Custom icons are copied into QuickAction's local data folder.
- Pin up to five apps. The remaining shortcuts are available in All Apps. Press Escape or click outside to close All Apps.
- The system tray menu provides Show / hide dock, Settings, and Exit.
- Start with Windows is off by default. If you move the application folder, turn this setting off and on again.

The dock expands and collapses over 400 ms, easing gently at the end and staying attached to the screen edge. Animations are disabled when Windows motion effects are turned off. Animation changes the visible region inside a fixed native window; the native window size is finalized after the animation completes.

## Building, testing, and packaging

Build on Windows x64 with .NET SDK 10 and Visual Studio Build Tools with the Desktop development with C++ workload, C++20 support, and the Windows 11 SDK. The native backdrop build uses Windows PowerShell by its full path; PowerShell 7 does not need to be on Visual Studio's PATH. The native component builds automatically and its DLL is bundled with the app. Release users do not need build tools installed.

```powershell
dotnet build QuickAction/QuickAction.csproj -c Release
dotnet run --project QuickAction.Tests -c Release
./publish.ps1
```

For Microsoft Store CI/CD, see the [setup guide](docs/MICROSOFT-STORE.md). Pushes and pull requests run builds and tests. Pushes to `main` submit the MSIX package once Partner Center and GitHub secrets are configured.

Core tests use an executable harness without an external test framework. `QuickAction.Core` contains models, validation, launch-command construction, pinning, geometry, and persistence. `QuickAction` contains the view model, WPF views, and Windows integration. Launch-command construction is separate from process execution so it can be tested without running programs.

## Local data and backups

Configuration is stored at `%LocalAppData%\QuickAction\config.json`, with icons in the adjacent `icons` folder. Saving retains a `.bak` file. Damaged configuration files are preserved before recovery.

JSON backups contain configuration and embedded base64 icons, but not the target programs. Older ZIP backups can still be imported. Arguments are stored verbatim and may contain sensitive information. Error logs record only exception types and HRESULT values, with at most two files of approximately 1 MB each.

The data schema is documented in [docs/quickaction.schema.json](docs/quickaction.schema.json). Current configuration and backup files use an envelope with `format: quickaction`, `schemaVersion: 1`, `configuration`, and `assets`. Older data remains readable and is migrated when saved. Local configuration keeps icons in the `icons` folder; JSON backups embed them in `assets`.

QuickAction has no automatic updater, cloud synchronization, or telemetry. Version 1 supports one dock, up to five pinned apps, and additional shortcuts in All Apps.

## Launch behavior

- EXE files run directly.
- BAT and CMD files run through `cmd.exe /d /s /c`.
- PS1 files run through Windows PowerShell with `-NoProfile -File`.
- MSI files run through `msiexec /i`.

Arguments follow the target program's native parsing rules. BAT and CMD arguments have shell semantics, so characters such as `&`, `|`, and `%` are interpreted by CMD. QuickAction does not change PowerShell ExecutionPolicy, bypass UAC, or hide console windows.

A “Launch requested” notification means the launch request was sent, not that the program finished. PowerShell scripts that exit with a nonzero code generate a separate notification.

## Rendering and visual checks

The dock uses a layered WPF window with per-pixel alpha. Windows Composition draws blur and tint in the same background layer with rounded clipping. Position, size, and corner radius follow the dock's expansion and collapse. Whole-window accent blur is not used because it ignores the layered window's clipping region.

The tint follows the opacity setting; 100% opacity completely covers the blur. When transparency is disabled, high contrast is enabled, or blur is unsupported, the dock uses an opaque background. Dock placement uses physical screen pixels, while interface dimensions use device-independent pixels (DIP). Always on top is enabled by default and can be turned off in Settings.

Run visual regression checks in an interactive Windows desktop session:

```powershell
dotnet build QuickAction.VisualTests -c Release -m:1
dotnet run --project QuickAction.VisualTests -c Release --no-build
dotnet run --project QuickAction.VisualTests -c Release --no-build -- --blur
dotnet run --project QuickAction.VisualTests -c Release --no-build -- --settings
```

The transparency harness uses a separate temporary configuration, checks the HWND layered flag, and renders 36 cases: four screen edges, three states, and three DPI levels. It verifies that corner alpha is zero while the center remains visible.

The `--blur` check temporarily displays a striped background to compare desktop pixel contrast with blur enabled and disabled. It also checks pixels outside the rounded corners in idle and All Apps states, and at 10%, 35%, and 70% of expansion and collapse on all four edges. Windows transparency must be enabled and high contrast disabled. This check measures the Windows Composition layer, which WPF's RenderTargetBitmap does not capture.

The `--settings` check covers the settings interface, language switching, immediate preference updates, persistence, input validation, and shortcut operations.

## Manual checks before a broad release

- All four dock edges, the 150 ms hover delay, the default 600 ms collapse delay, All Apps, and keyboard interaction.
- Two monitors with different DPI scales (100%, 150%, and 200%), negative screen coordinates, monitor removal, and position persistence after restart.
- Multiple shortcuts for the same app with different arguments, custom icons, the five-app pin limit, and drag reordering.
- Harmless sample files of each supported type with spaces and Unicode, administrator launch, UAC cancellation, and PowerShell policy restrictions.
- Transparency disabled, high contrast enabled, no mouse interaction outside rounded corners, and idle CPU usage.
- Backup and restore, damaged configuration recovery, the system tray, startup, and launching a second instance.
