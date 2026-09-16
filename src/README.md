# llama.cpp Launcher

A Windows tray launcher for `llama-server.exe`. See `../SPEC.md` for the full
feature specification.

## Download

Latest release: [v1.0](https://github.com/ldd-palm/llamacpp-launcher/releases/tag/v1.0)

| Package | Size | Requires |
|---|---|---|
| [LlamaCppLauncher-v1.0-win-x64-framework-dependent.zip](https://github.com/ldd-palm/llamacpp-launcher/releases/download/v1.0/LlamaCppLauncher-v1.0-win-x64-framework-dependent.zip) | ~2.9 MB | .NET 8 Desktop Runtime installed |
| [LlamaCppLauncher-v1.0-win-x64-self-contained.zip](https://github.com/ldd-palm/llamacpp-launcher/releases/download/v1.0/LlamaCppLauncher-v1.0-win-x64-self-contained.zip) | ~68 MB | Nothing — runtime is bundled |

## Build

```
dotnet build LlamaCppLauncher.sln
```

## Run (debug)

```
dotnet run --project LlamaCppLauncher
```

## Test

```
dotnet test LlamaCppLauncher.sln
```

## Publish a self-contained single-file build

```
dotnet publish LlamaCppLauncher/LlamaCppLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

The output `publish/LlamaCppLauncher.exe` runs on a machine with no .NET runtime
installed. It's portable: `config.json` and the `logs\` folder are created next to
the exe itself, not under `%LOCALAPPDATA%`, so the whole `publish\` folder can be
copied/moved as a unit. On first launch (no `config.json` yet next to the exe), it
opens the Settings window automatically — point "llama.cpp executable path" at your
`llama-server.exe`, "Models directory" at a folder of `.gguf` files, pick a Default
Model on the Models page, and Save.
