# llama.cpp Launcher

A Windows tray launcher for `llama-server.exe`: start/stop the server, switch
between local GGUF models, and manage per-model launch parameters, all from
the system tray. See [`SPEC.md`](SPEC.md) for the full feature specification
and [`src/README.md`](src/README.md) for build/run/test/publish commands.

## Download

Latest release: [v1.0](https://github.com/ldd-palm/llamacpp-launcher/releases/tag/v1.0)

| Package | Size | Requires |
|---|---|---|
| [LlamaCppLauncher-v1.0-win-x64-framework-dependent.zip](https://github.com/ldd-palm/llamacpp-launcher/releases/download/v1.0/LlamaCppLauncher-v1.0-win-x64-framework-dependent.zip) | ~2.9 MB | .NET 8 Desktop Runtime installed |
| [LlamaCppLauncher-v1.0-win-x64-self-contained.zip](https://github.com/ldd-palm/llamacpp-launcher/releases/download/v1.0/LlamaCppLauncher-v1.0-win-x64-self-contained.zip) | ~68 MB | Nothing — runtime is bundled |

The app is portable: `config.json` and `logs\` are created next to the exe
itself, not under `%LOCALAPPDATA%`, so the extracted folder can be
copied/moved as a unit. On first launch (no `config.json` yet next to the
exe), it opens the Settings window automatically — point "llama.cpp
executable path" at your `llama-server.exe`, "Models directory" at a folder
of `.gguf` files, pick a Default Model on the Models page, and Save.
