# llama.cpp Launcher — Specification

## 1. Overview

A Windows tray-resident launcher for `llama-server.exe`. It replaces the interactive
`llama-start.bat` console workflow with a persistent system-tray application: load a
model, manage the server lifecycle, and switch between configured models — all via a
right-click tray menu, with a Fluent Design settings UI for configuration.

UI language: **English** throughout (menu items, window text, tooltips, notifications).

## 2. Technology & Architecture

- **Stack**: C# / .NET 8, WPF + [WPF-UI](https://github.com/lepoco/wpfui) (Fluent Design
  component library — Mica/Acrylic, rounded corners, light/dark theme follows Windows).
  Tray icon via WPF-UI's NotifyIcon support.
- **Process model**: The launcher is a tray-only WPF app with no main window
  (`ShutdownMode=OnExplicitShutdown`). It starts/stops `llama-server.exe` directly via
  `System.Diagnostics.Process` (no PowerShell wrapper), redirecting stdout/stderr to log
  files.
- **Single instance**: Enforced via a named Mutex. A second launch activates/no-ops
  against the existing instance instead of starting a duplicate.
- **Config file**: JSON at `config.json`, next to the launcher executable (portable —
  travels with the exe rather than living under `%LOCALAPPDATA%`). Read automatically
  on launcher startup. Written only when the user clicks Save in Settings.
- **Logs**: `logs\llama-server.out.log` and `llama-server.err.log`, also next to the exe.
- **Autostart**: Written to `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
  (user-scope, no admin rights required), pointing to the launcher executable.
- **Model discovery**: Non-recursive scan of the configured Models directory for `*.gguf`
  files (matches the existing bat script's behavior).

## 3. Tray Icon & Main Menu

### Icon states
- Service **On** → `images\cpp_logo_color.ico`
- Service **Off** → `images\cpp_logo_bw.ico`
- Tooltip: `llama.cpp Launcher — Running (<Alias>)` or `llama.cpp Launcher — Stopped`

### Menu items (top to bottom)

1. **Service: Off/On** — click toggles directly.
   - Off → On: starts the server using the configured **Default Model**.
   - On → Off: stops the running `llama-server` process.
2. **Settings** — opens the Settings window (§4).
3. **Switch ▶** — expands a submenu listing all models defined in `config.json` (by
   Alias). The currently running model is highlighted (accent-colored background + bullet
   marker); others are plain text. Clicking another model stops the current server and
   starts the selected one, updating the "last running model" state in config.
   - **Disabled (greyed out) when Service is Off** — switching has no meaning if nothing
     is running.
4. **About** — opens the About window (§5).
5. **Exit** — stops `llama-server` (if running) and fully exits the launcher. There is no
   "leave server running in background" option.

### Launcher startup behavior

On launcher process start:
- If `config.json` exists and validates (exe path exists, models dir has ≥1 `.gguf`,
  port free / usable), automatically load the Default Model and start the service
  (tray icon starts colored — equivalent to "boot and go").
- If `config.json` is missing, corrupted, or fails validation, the launcher starts in the
  **Off** state and — for missing/corrupted config — opens Settings automatically with an
  error banner; for validation failures on an otherwise valid config, it stays Off and
  shows a balloon notification describing the problem (see §6).

## 4. Settings Window

Fluent Design window with a left-hand navigation rail (**General**, **Models**) and a
right-hand content pane.

### 4.1 General page

| Field | Description |
|---|---|
| llama.cpp executable path | Path to `llama-server.exe` (or `server.exe`); browsable. Inline icon shows real-time validation (file exists / not found). |
| Models directory | Directory containing `.gguf` files (root-level scan only); browsable. Inline validation shows "Found N models" or an error if the directory is missing/empty. |
| Port | Numeric input. Real-time check distinguishes "in use by this launcher's own llama-server" vs. "in use by another process" (the latter is an error state). |
| Start with Windows | Checkbox; toggling immediately reads/writes the Run registry key. |

Validation is **real-time** (on field change / focus-loss) — errors are surfaced inline
without requiring a save. Values only take effect in `config.json` when the user clicks
**Save**.

### 4.2 Models page

- Dropdown to select one discovered `.gguf` file.
- Per-model parameter form (a newly-discovered model is pre-filled with the defaults
  below, editable per model):
  - **Alias**
  - **CTX_SIZE**
  - **N_GPU_LAYERS**
  - **KV cache quantization** (`-ctk` / `-ctv`, dropdown: e.g. `q8_0`, `f16`, `q4_0`)
  - **Threads** (`--threads`)
  - **Batch size** (`-b`)
  - **Flash Attention** (checkbox → `--flash-attn`)
  - **Default Model** (single global radio — exactly one model can be Default at a time;
    selecting a new Default clears the flag on any previously-selected model)
  - **Extra command-line arguments** (free-text field for any flag not covered above,
    appended verbatim to the launch command)
- Switching the dropdown to a different model while the current form has unsaved edits
  prompts the user before discarding changes.
- **Save this model's configuration** writes/updates that model's entry in `config.json`.

#### Parameter defaults & descriptions

| Parameter | Flag | Default | Description |
|---|---|---|---|
| Alias | `--alias` | .gguf filename without extension | Friendly name shown in the Switch menu, About window, and API responses. |
| CTX_SIZE | `-c` | `8192` | Context window size (max tokens of prompt + generation). llama-server's own built-in default is 4096; the launcher defaults higher for more headroom. |
| N_GPU_LAYERS | `-ngl` | `0` | Number of model layers offloaded to GPU. `0` = CPU-only inference, the safest default across GPUs/drivers; increase to offload layers to GPU once VRAM/driver stability is confirmed. |
| KV cache quantization | `-ctk` / `-ctv` | `q8_0` | Precision used to store the attention KV cache. `q8_0` cuts memory use substantially versus full precision (`f16`) with minimal quality loss. |
| Threads | `--threads` | *Auto* (field left blank) | CPU threads used for inference. Left blank tells llama-server to auto-detect based on available cores; set explicitly to pin a thread count. |
| Batch size | `-b` | `2048` | Max tokens processed per batch during prompt evaluation. Higher values can speed up prompt processing at the cost of more memory. |
| Flash Attention | `--flash-attn` | Off (unchecked) | Enables the Flash Attention kernel. Can reduce memory use and improve speed when the backend/model supports it; left off by default for broadest compatibility. |
| Default Model | — | None until set | No model is Default out of the box — the user must designate exactly one before the launcher can auto-start on boot / Service On. |
| Extra command-line arguments | — | *(empty)* | Free-text flags appended verbatim to the launch command; empty by default. |

### 4.3 Save semantics

- Settings changes (General or Models) are only persisted on explicit Save.
- If the user saves changes to the parameters of the **currently running** model, the
  launcher does **not** auto-restart the server (to avoid dropping in-flight requests). It
  shows a notice: "Changes will apply the next time this model is started."

## 5. About Window

Shows information about the currently running model, queried from `GET /v1/models` on
the running server (same approach as the existing bat script's status check).

| Field | Source |
|---|---|
| File name | Local config (the `.gguf` filename) |
| Alias | Local config / API `data[0].id` |
| Web Chat URL | `http://{host}:{port}/` |
| API URL | `http://{host}:{port}/v1` |
| Quantization | API `data[0].meta.ftype` |
| Total Params | API `data[0].meta.n_params`, formatted (e.g. "7.62 B") |
| Context Size | API `data[0].meta.n_ctx` |

If Service is Off, the window shows a "No model is currently running" state instead of
the table above.

At the bottom of the window: the llama.cpp logo (`images\llamacpp.png`) as a clickable
link to the project homepage (`https://github.com/ggml-org/llama.cpp`).

## 6. Error Handling & Edge Cases

- **Startup validation failure** (exe path invalid, models dir empty, or port occupied by
  an unrelated process): launcher stays Off, tray icon stays black-and-white, and a
  balloon notification states the specific problem.
- **Port occupied by an unrelated process**: flagged in Settings with an inline
  warning; attempting to turn Service On is blocked and reports the same error via
  balloon notification.
- **`llama-server` crashes unexpectedly** while Service shows On: launcher detects process
  exit (via `Process.Exited` / polling), flips state back to Off (bw icon), and shows a
  balloon notification pointing to the error log.
- **Switching models fails to start** (bad params, OOM, etc.): the launcher does not fall
  back to the previous model. If the new process fails to bind the port within ~10
  seconds, it reports failure via balloon notification and leaves Service in the Off
  state.
- **Config file missing / corrupted at launch**: treated as first run — Settings opens
  automatically with an error banner; the launcher does not crash.

## 7. Out of Scope (for this spec)

- Remote/network management of the launcher (it is local-machine only).
- Running multiple models/ports concurrently.
- Auto-update mechanism for the launcher itself.
- Log rotation policy (implementation detail, not an architectural decision).
