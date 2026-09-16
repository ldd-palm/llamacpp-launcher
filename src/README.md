# llama.cpp Launcher

A Windows tray launcher for `llama-server.exe`. See `../SPEC.md` for the full
feature specification.

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
