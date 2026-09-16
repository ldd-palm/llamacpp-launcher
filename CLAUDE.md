# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

Despite the folder name, this is **not** the upstream llama.cpp C/C++ project. It is a
standalone Windows tray launcher app for `llama-server.exe`, written in C#. The full
feature spec is in `SPEC.md` at the repo root — read it for intended UX/behavior before
making non-trivial changes. Note: several later UI iterations (tabs instead of a nav rail,
"About" renamed to "Status", the "Extra command-line arguments" field replaced by
Generate+editable-CommandLine) diverged from what `SPEC.md` still describes — when in
doubt, trust the code over the spec for anything UI-related.

`docs/superpowers/plans/2026-08-23-llamacpp-launcher-design.md` is the original 23-task
implementation plan the app was built from task-by-task; useful for historical context on
why a given class exists, but not authoritative for current behavior.

`llama-start.bat` is the older interactive console script this launcher replaces.

## Commands

All commands run from `src/`.

```
dotnet build LlamaCppLauncher.sln              # build
dotnet test LlamaCppLauncher.sln               # run all tests
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"   # run a single test
dotnet run --project LlamaCppLauncher          # run in debug
```

Publish variants (all from `src/`):

```
# Self-contained single-file — no .NET runtime required on the target machine, ~160MB+
dotnet publish LlamaCppLauncher/LlamaCppLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# Framework-dependent, multi-file — target machine needs .NET 8 Desktop Runtime
dotnet publish LlamaCppLauncher/LlamaCppLauncher.csproj -c Release -r win-x64 --self-contained false -o publish-fd

# Framework-dependent, single-file — same runtime requirement, bundled into one exe
dotnet publish LlamaCppLauncher/LlamaCppLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish-single-fd
```

The app is portable: `config.json` and `logs\` are created next to whatever exe is
running (`AppContext.BaseDirectory`), not under `%LOCALAPPDATA%`. A publish output folder
can be copied/moved as a self-contained unit. Before re-publishing, check
`tasklist //FI "IMAGENAME eq LlamaCppLauncher.exe"` — the publish overwrite fails if a
previous build is still running from that output folder.

## Architecture

**Composition root:** `App.xaml.cs`. No main window (`ShutdownMode=OnExplicitShutdown`
in `App.xaml`) — the app lives entirely in the tray. `OnStartup` wires every service
together manually (plain `new`, no DI container) and hands them to `TrayController`.

**Two-tier testing split**, deliberate and load-bearing — don't add tests for the second
tier, and don't skip tests for the first:
- **Plain, unit-tested classes** (`Config/`, `Discovery/`, `Server/` except the process
  manager's actual process spawning, `Validation/`, `AutoStart/`, `Startup/`, the
  `Settings/*ViewModel.cs` and `About/AboutViewModel.cs`) — no WPF/WinForms dependency,
  fully covered by `LlamaCppLauncher.Tests`.
- **WPF/WinForms-facing glue** (`TrayController`, `App.xaml.cs`, the `*Window.xaml.cs`
  code-behinds, `TrayContextMenuHost`) — thin composition wiring real OS UI surfaces that
  can't run headless in this environment. Verified by `dotnet build` plus manual smoke
  testing only, not unit tests.

**Tray icon + menu split across two UI frameworks on purpose:**
`System.Windows.Forms.NotifyIcon` (in `TrayController`) is the tray icon/balloon
notifications — it's the only Win32-backed API that supports a persistent tray icon and
balloon tips without a host window. The right-click context menu, however, is a fully
custom-templated WPF `ContextMenu` (styles in `Tray/TrayContextMenuStyles.xaml`) so it can
get proper Fluent styling (rounded corners, drop shadow, submenu flyouts) that
`ContextMenuStrip` can't do. Since a WinForms `NotifyIcon` has no `PresentationSource` of
its own, `TrayContextMenuHost` keeps a permanently-alive, invisible, zero-size WPF
`Window` around purely so the `ContextMenu` has a live host to open against.

**Model launch command resolution** (`Server/LlamaServerArgumentBuilder.cs`): a
`ModelProfile` (`Config/ModelProfile.cs`) has both structured fields (CtxSize,
NGpuLayers, KvCacheType, BatchSize, FlashAttention, ...) *and* an optional `CommandLine`
string. `Build()` uses `CommandLine` verbatim (tokenized with quote-aware splitting) if
it's non-empty; otherwise it assembles a command from the structured fields. The Models
settings page's "Generate" button calls `GenerateCommandLine()` to render the structured
fields into that editable string — after Generate, the structured fields become inert
until the CommandLine box is cleared. `ModelProfile` is a CommunityToolkit.Mvvm
`ObservableObject` specifically so edits made in one place (e.g. the detail pane) don't
produce a stale/inconsistent view elsewhere (e.g. the model picker dropdown).

**Startup decision flow:** `Startup/LauncherBootstrapper.Decide()` is a pure function
(`AppConfig? → StartupDecision`) that decides whether to auto-start the Default Model,
stay off with a notification, or open Settings with an error — `App.xaml.cs` just acts on
whatever it returns. This keeps the "what should happen on launch" logic unit-testable
separately from the WPF startup sequence.

**Config persistence:** `Config/ConfigService.cs` does plain `System.Text.Json`
read/write of `AppConfig` (no source-generated JSON context) to `config.json` next to the
exe. Config is loaded once at startup and only re-read after a Settings save
(`TrayController.ReloadConfigAfterSettingsSaved`) — there's no file-watcher.

**Port ownership disambiguation:** `Validation/PortCheckService.cs` distinguishes "port
occupied by this launcher's own tracked llama-server process" from "occupied by an
unrelated program" by comparing the listening process's module path against the
configured executable path (passed as a `Func<string>` so it stays live if the user edits
the path in Settings without restarting the app).

**Settings window** (`Settings/SettingsWindow.xaml` + `SettingsViewModel` +
`GeneralSettingsViewModel` + `ModelsSettingsViewModel`): General and Models are two panes
toggled by a bool on `SettingsViewModel`, not an actual `TabControl`. `SettingsViewModel`
owns cross-pane coordination (e.g. changing the Models directory in General triggers
`ModelsSettingsViewModel.RefreshFromDirectory`) and raises events
(`StartModelRequested`, `StopRequested`, `StatusRequested`) that `App.xaml.cs` subscribes
to, since the ViewModel itself has no reference to `TrayController`.
