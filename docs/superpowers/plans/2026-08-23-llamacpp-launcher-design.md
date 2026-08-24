# llama.cpp Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows tray-resident launcher for `llama-server.exe` per `SPEC.md` — start/stop/switch models from a right-click tray menu, with a Fluent Design Settings window and an About window, replacing the interactive `llama-start.bat` workflow.

**Architecture:** C# / .NET 8 WPF app (`src/LlamaCppLauncher`). The system tray icon, its context menu, and balloon notifications are implemented with `System.Windows.Forms.NotifyIcon` (the only Win32 API that supports balloon tips and a persistent tray icon without a host window); the Settings and About windows are WPF-UI (`Wpf.Ui.Controls.FluentWindow`) dialogs for the Fluent Design look. All decision logic (config load/save, validation, port checks, argument building, startup behavior, tray menu state, view-model validation/formatting) lives in plain, unit-tested C# classes with no WPF/WinForms dependency; the WPF/WinForms-facing classes (`TrayController`, `App`, the two `Window` code-behinds) are thin composition/glue verified by `dotnet build` plus a final manual smoke test, since they wire together already-tested logic to real OS UI surfaces that can't be unit tested in this environment.

**Tech Stack:** .NET 8 (`net8.0-windows`), WPF, `WPF-UI` 4.3.0 (Fluent Design controls for Settings/About), `System.Windows.Forms.NotifyIcon` (tray icon — project has `UseWindowsForms` enabled alongside `UseWPF`), `CommunityToolkit.Mvvm` 8.4.2 (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`), xUnit for tests.

All code samples in this plan were validated against the real toolchain during planning (`dotnet build`/`dotnet test`/`dotnet run` in scratch projects) — the package versions, XAML control names, and API surfaces below are confirmed to exist and compile, not guessed.

---

## Solution layout (reference — built up task by task)

```
llama.cpp/                                  (repo root)
  SPEC.md
  llama-start.bat
  images/
  src/
    LlamaCppLauncher.sln
    LlamaCppLauncher/
      LlamaCppLauncher.csproj
      App.xaml / App.xaml.cs
      Config/            AppConfig.cs, ModelProfile.cs, ConfigService.cs
      Discovery/         ModelDiscoveryService.cs
      Server/            LlamaServerArgumentBuilder.cs, LlamaServerProcessManager.cs,
                          LlamaServerApiClient.cs, ModelInfo.cs, ParamFormatter.cs
      Validation/         PortStatus.cs, PortStatusClassifier.cs, ITcpPortProbe.cs, TcpPortProbe.cs,
                          IPortChecker.cs, PortCheckService.cs, ValidationResult.cs, ValidationService.cs
      AutoStart/         IRunKeyStore.cs, RegistryRunKeyStore.cs, AutoStartService.cs
      Startup/           StartupAction.cs, StartupDecision.cs, LauncherBootstrapper.cs
      Tray/              ServiceState.cs, TrayMenuState.cs, TrayMenuStateBuilder.cs,
                          SingleInstanceService.cs, TrayController.cs
      Settings/          GeneralSettingsViewModel.cs, ModelsSettingsViewModel.cs,
                          SettingsViewModel.cs, SettingsWindow.xaml/.xaml.cs
      About/             AboutViewModel.cs, InverseBooleanToVisibilityConverter.cs,
                          AboutWindow.xaml/.xaml.cs
    LlamaCppLauncher.Tests/
      LlamaCppLauncher.Tests.csproj
      Config/, Discovery/, Server/, Validation/, AutoStart/, Startup/, Tray/, Settings/, About/, TestSupport/
  docs/superpowers/plans/2026-08-23-llamacpp-launcher-design.md   (this file)
```

---

### Task 1: Solution & project scaffolding

**Files:**
- Create: `src/LlamaCppLauncher.sln`
- Create: `src/LlamaCppLauncher/LlamaCppLauncher.csproj`
- Create: `src/LlamaCppLauncher.Tests/LlamaCppLauncher.Tests.csproj`
- Delete: `src/LlamaCppLauncher/MainWindow.xaml`, `src/LlamaCppLauncher/MainWindow.xaml.cs` (template default, unused — the app has no main window, only tray + on-demand dialogs)
- Delete: `src/LlamaCppLauncher.Tests/UnitTest1.cs` (template placeholder)

- [ ] **Step 1: Scaffold the solution and both projects**

Run from the repo root (`C:\Users\ldd\Documents\Works\llama.cpp`):

```bash
mkdir src
cd src
dotnet new sln -n LlamaCppLauncher
dotnet new wpf -n LlamaCppLauncher -o LlamaCppLauncher -f net8.0
dotnet new xunit -n LlamaCppLauncher.Tests -o LlamaCppLauncher.Tests
dotnet sln add LlamaCppLauncher/LlamaCppLauncher.csproj LlamaCppLauncher.Tests/LlamaCppLauncher.Tests.csproj
rm LlamaCppLauncher/MainWindow.xaml LlamaCppLauncher/MainWindow.xaml.cs
rm LlamaCppLauncher.Tests/UnitTest1.cs
```

Note: `dotnet new wpf -f net8.0` produces a `TargetFramework` of `net8.0-windows` automatically (WPF always implies the Windows-specific TFM) — this was confirmed by actually running the command during planning.

- [ ] **Step 2: Add packages to the main project**

```bash
dotnet add LlamaCppLauncher/LlamaCppLauncher.csproj package WPF-UI --version 4.3.0
dotnet add LlamaCppLauncher/LlamaCppLauncher.csproj package CommunityToolkit.Mvvm --version 8.4.2
```

- [ ] **Step 3: Edit `LlamaCppLauncher/LlamaCppLauncher.csproj` to its final form**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>LlamaCppLauncher</RootNamespace>
    <AssemblyName>LlamaCppLauncher</AssemblyName>
    <ApplicationIcon>..\..\images\cpp_logo_color.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="WPF-UI" Version="4.3.0" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
  </ItemGroup>

  <ItemGroup>
    <EmbeddedResource Include="..\..\images\cpp_logo_color.ico" Link="Assets\cpp_logo_color.ico" LogicalName="LlamaCppLauncher.Assets.cpp_logo_color.ico" />
    <EmbeddedResource Include="..\..\images\cpp_logo_bw.ico" Link="Assets\cpp_logo_bw.ico" LogicalName="LlamaCppLauncher.Assets.cpp_logo_bw.ico" />
    <Resource Include="..\..\images\llamacpp.png" Link="Assets\llamacpp.png" />
  </ItemGroup>

</Project>
```

`UseWindowsForms` is required because the tray icon uses `System.Windows.Forms.NotifyIcon` (see Task 18) — `Wpf.Ui`'s own tray control has no balloon-notification API, and SPEC.md §6 requires balloon notifications. The two `.ico` files are `EmbeddedResource` (loaded via `Assembly.GetManifestResourceStream` into a `System.Drawing.Icon` for the tray icon); `llamacpp.png` is a WPF `Resource` (loaded via a `pack://application:,,,/Assets/llamacpp.png` URI by an `<Image>` in the About window). Both patterns were verified to build and load correctly at runtime during planning.

- [ ] **Step 4: Edit `LlamaCppLauncher.Tests/LlamaCppLauncher.Tests.csproj`**

Change `<TargetFramework>net10.0</TargetFramework>` to `<TargetFramework>net8.0-windows</TargetFramework>` (must match the main project's TFM family for the project reference to resolve — a plain `net8.0` test project cannot reference a `net8.0-windows` project, confirmed during planning), and add a project reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\LlamaCppLauncher\LlamaCppLauncher.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Build and verify**

```bash
dotnet build src/LlamaCppLauncher.sln
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add src .gitignore
git commit -m "Scaffold LlamaCppLauncher WPF solution with WPF-UI, WinForms tray support, and xUnit test project"
```

(Add a `src/.gitignore` — or entries in the repo-root `.gitignore` — for `bin/`, `obj/`, and `publish/` before committing, e.g. append `**/bin/`, `**/obj/`, `src/**/publish/` to the existing `.gitignore`.)

---

### Task 2: Config models — `AppConfig`, `ModelProfile`

**Files:**
- Create: `src/LlamaCppLauncher/Config/AppConfig.cs`
- Create: `src/LlamaCppLauncher/Config/ModelProfile.cs`
- Test: `src/LlamaCppLauncher.Tests/Config/ModelProfileDefaultsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// src/LlamaCppLauncher.Tests/Config/ModelProfileDefaultsTests.cs
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Tests.Config;

public class ModelProfileDefaultsTests
{
    [Fact]
    public void CreateDefault_UsesFileNameWithoutExtensionAsAlias()
    {
        var profile = ModelProfile.CreateDefault("Qwen2.5-7B-Instruct-Q4_K_M.gguf");

        Assert.Equal("Qwen2.5-7B-Instruct-Q4_K_M.gguf", profile.FileName);
        Assert.Equal("Qwen2.5-7B-Instruct-Q4_K_M", profile.Alias);
    }

    [Fact]
    public void CreateDefault_MatchesSpecDefaultValues()
    {
        var profile = ModelProfile.CreateDefault("model.gguf");

        Assert.Equal(8192, profile.CtxSize);
        Assert.Equal(0, profile.NGpuLayers);
        Assert.Equal("q8_0", profile.KvCacheType);
        Assert.Null(profile.Threads);
        Assert.Equal(2048, profile.BatchSize);
        Assert.False(profile.FlashAttention);
        Assert.False(profile.IsDefault);
        Assert.Equal(string.Empty, profile.ExtraArguments);
    }
}
```

- [ ] **Step 2: Run test to verify it fails to compile (types don't exist yet)**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelProfileDefaultsTests`
Expected: build error, `ModelProfile` does not exist in namespace `LlamaCppLauncher.Config`.

- [ ] **Step 3: Create `AppConfig.cs`**

```csharp
// src/LlamaCppLauncher/Config/AppConfig.cs
namespace LlamaCppLauncher.Config;

public sealed class AppConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string ModelsDirectory { get; set; } = string.Empty;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool StartWithWindows { get; set; }
    public string? LastRunningModelFileName { get; set; }
    public List<ModelProfile> Models { get; set; } = new();
}
```

- [ ] **Step 4: Create `ModelProfile.cs`**

```csharp
// src/LlamaCppLauncher/Config/ModelProfile.cs
namespace LlamaCppLauncher.Config;

public sealed class ModelProfile
{
    public string FileName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public int CtxSize { get; set; } = 8192;
    public int NGpuLayers { get; set; }
    public string KvCacheType { get; set; } = "q8_0";
    public int? Threads { get; set; }
    public int BatchSize { get; set; } = 2048;
    public bool FlashAttention { get; set; }
    public bool IsDefault { get; set; }
    public string ExtraArguments { get; set; } = string.Empty;

    public static ModelProfile CreateDefault(string fileName)
    {
        return new ModelProfile
        {
            FileName = fileName,
            Alias = Path.GetFileNameWithoutExtension(fileName),
            CtxSize = 8192,
            NGpuLayers = 0,
            KvCacheType = "q8_0",
            Threads = null,
            BatchSize = 2048,
            FlashAttention = false,
            IsDefault = false,
            ExtraArguments = string.Empty
        };
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelProfileDefaultsTests`
Expected: `Passed! - Failed: 0, Passed: 2`.

- [ ] **Step 6: Commit**

```bash
git add src/LlamaCppLauncher/Config src/LlamaCppLauncher.Tests/Config
git commit -m "Add AppConfig and ModelProfile models with spec-matching defaults"
```

---

### Task 3: `ConfigService` (load/save JSON config)

**Files:**
- Create: `src/LlamaCppLauncher/Config/ConfigService.cs`
- Test: `src/LlamaCppLauncher.Tests/Config/ConfigServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Config/ConfigServiceTests.cs
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Tests.Config;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        var service = new ConfigService(_configPath);

        Assert.Null(service.Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsConfig()
    {
        var service = new ConfigService(_configPath);
        var config = new AppConfig
        {
            ExecutablePath = @"C:\llama\llama-server.exe",
            ModelsDirectory = @"C:\llama\models",
            Port = 8080,
            StartWithWindows = true,
            Models = { ModelProfile.CreateDefault("model.gguf") }
        };

        service.Save(config);
        AppConfig? loaded = service.Load();

        Assert.NotNull(loaded);
        Assert.Equal(config.ExecutablePath, loaded!.ExecutablePath);
        Assert.Equal(config.ModelsDirectory, loaded.ModelsDirectory);
        Assert.Equal(config.Port, loaded.Port);
        Assert.True(loaded.StartWithWindows);
        Assert.Single(loaded.Models);
        Assert.Equal("model.gguf", loaded.Models[0].FileName);
    }

    [Fact]
    public void Load_ThrowsConfigLoadException_WhenFileIsNotValidJson()
    {
        File.WriteAllText(_configPath, "{ not valid json");
        var service = new ConfigService(_configPath);

        Assert.Throws<ConfigLoadException>(() => service.Load());
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenMissing()
    {
        string nestedPath = Path.Combine(_tempDir, "nested", "config.json");
        var service = new ConfigService(nestedPath);

        service.Save(new AppConfig());

        Assert.True(File.Exists(nestedPath));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ConfigServiceTests`
Expected: build error, `ConfigService` / `ConfigLoadException` do not exist.

- [ ] **Step 3: Implement `ConfigService.cs`**

```csharp
// src/LlamaCppLauncher/Config/ConfigService.cs
using System.IO;
using System.Text.Json;

namespace LlamaCppLauncher.Config;

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _configFilePath;

    public ConfigService(string configFilePath)
    {
        _configFilePath = configFilePath;
    }

    public AppConfig? Load()
    {
        if (!File.Exists(_configFilePath))
        {
            return null;
        }

        string json;
        try
        {
            json = File.ReadAllText(_configFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigLoadException("Configuration file could not be read.", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json)
                ?? throw new ConfigLoadException("Configuration file is empty.", new InvalidDataException());
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException("Configuration file is not valid JSON.", ex);
        }
    }

    public void Save(AppConfig config)
    {
        string? directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(_configFilePath, json);
    }
}
```

Note for this file and every later production file (not test file) in `src/LlamaCppLauncher` that uses `File`/`Directory`/`Path`/`Stream`: this project has `UseWPF` and `UseWindowsForms` both enabled, which changes the SDK's implicit-usings set to `System`, `System.Collections.Generic`, `System.Drawing`, `System.Linq`, `System.Threading`, `System.Threading.Tasks`, `System.Windows.Forms` — **`System.IO` is not in that list**, unlike a plain library/test project. An explicit `using System.IO;` (as added above) is required wherever this project's code uses those types; the test project doesn't have this problem since it isn't a WPF/WinForms project. Later task code blocks in this plan already include `using System.IO;` where needed for this same reason.

`Load()` wraps two different failure sources into `ConfigLoadException`: the `File.ReadAllText` call (an unreadable file — locked by another process, permissions denied) and the JSON parse itself. This matters because every caller in this plan (Task 11's `LauncherBootstrapper`, Task 21's `App.xaml.cs`) only catches `ConfigLoadException`, not raw `IOException`/`UnauthorizedAccessException` — without this wrapping, a locked or permission-denied config file would crash the app at startup with an unhandled exception instead of degrading to the same "corrupted config, open Settings" behavior as a bad-JSON file. There's no dedicated unit test for this branch: reliably forcing an `IOException`/`UnauthorizedAccessException` from a hermetic test (without depending on OS-specific file-locking or permission behavior that isn't portable/reliable in CI) isn't worth the complexity here — the existing `Load_ThrowsConfigLoadException_WhenFileIsNotValidJson` test already proves the wrapping pattern works for the JSON-parse branch, and this branch is a straightforward, low-risk extension of the same pattern.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ConfigServiceTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Config/ConfigService.cs src/LlamaCppLauncher.Tests/Config/ConfigServiceTests.cs
git commit -m "Add ConfigService for loading and saving config.json"
```

---

### Task 4: `ModelDiscoveryService` (scan + merge .gguf files)

**Files:**
- Create: `src/LlamaCppLauncher/Discovery/ModelDiscoveryService.cs`
- Test: `src/LlamaCppLauncher.Tests/Discovery/ModelDiscoveryServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Discovery/ModelDiscoveryServiceTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;

namespace LlamaCppLauncher.Tests.Discovery;

public class ModelDiscoveryServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ModelDiscoveryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void DiscoverModelFiles_ReturnsOnlyTopLevelGgufFiles_SortedAlphabetically()
    {
        File.WriteAllText(Path.Combine(_tempDir, "b-model.gguf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "a-model.gguf"), "");
        File.WriteAllText(Path.Combine(_tempDir, "notes.txt"), "");
        string subDir = Path.Combine(_tempDir, "subfolder");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.gguf"), "");

        IReadOnlyList<string> result = ModelDiscoveryService.DiscoverModelFiles(_tempDir);

        Assert.Equal(new[] { "a-model.gguf", "b-model.gguf" }, result);
    }

    [Fact]
    public void DiscoverModelFiles_ReturnsEmpty_WhenDirectoryDoesNotExist()
    {
        IReadOnlyList<string> result = ModelDiscoveryService.DiscoverModelFiles(Path.Combine(_tempDir, "missing"));

        Assert.Empty(result);
    }

    [Fact]
    public void MergeWithConfiguredModels_PreservesExistingSettings_AndAddsDefaultsForNewFiles()
    {
        var existing = new ModelProfile { FileName = "a-model.gguf", Alias = "MyAlias", CtxSize = 16384 };

        List<ModelProfile> merged = ModelDiscoveryService.MergeWithConfiguredModels(
            new[] { "a-model.gguf", "b-model.gguf" },
            new[] { existing });

        Assert.Equal(2, merged.Count);
        Assert.Same(existing, merged[0]);
        Assert.Equal("MyAlias", merged[0].Alias);
        Assert.Equal(16384, merged[0].CtxSize);
        Assert.Equal("b-model", merged[1].Alias);
        Assert.Equal(8192, merged[1].CtxSize);
    }

    [Fact]
    public void MergeWithConfiguredModels_DropsEntriesForFilesNoLongerPresent()
    {
        var stale = new ModelProfile { FileName = "deleted.gguf" };

        List<ModelProfile> merged = ModelDiscoveryService.MergeWithConfiguredModels(
            new[] { "a-model.gguf" },
            new[] { stale });

        Assert.Single(merged);
        Assert.Equal("a-model.gguf", merged[0].FileName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelDiscoveryServiceTests`
Expected: build error, `ModelDiscoveryService` does not exist.

- [ ] **Step 3: Implement `ModelDiscoveryService.cs`**

```csharp
// src/LlamaCppLauncher/Discovery/ModelDiscoveryService.cs
using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Discovery;

public static class ModelDiscoveryService
{
    public static IReadOnlyList<string> DiscoverModelFiles(string modelsDirectory)
    {
        if (!Directory.Exists(modelsDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(modelsDirectory, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<ModelProfile> MergeWithConfiguredModels(
        IReadOnlyList<string> discoveredFileNames,
        IReadOnlyList<ModelProfile> configuredModels)
    {
        var byFileName = configuredModels.ToDictionary(m => m.FileName, StringComparer.OrdinalIgnoreCase);
        var merged = new List<ModelProfile>();

        foreach (string fileName in discoveredFileNames)
        {
            merged.Add(byFileName.TryGetValue(fileName, out ModelProfile? existing)
                ? existing
                : ModelProfile.CreateDefault(fileName));
        }

        return merged;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelDiscoveryServiceTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Discovery src/LlamaCppLauncher.Tests/Discovery
git commit -m "Add ModelDiscoveryService for scanning and merging .gguf model files"
```

---

### Task 5: `LlamaServerArgumentBuilder`

**Files:**
- Create: `src/LlamaCppLauncher/Server/LlamaServerArgumentBuilder.cs`
- Test: `src/LlamaCppLauncher.Tests/Server/LlamaServerArgumentBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Server/LlamaServerArgumentBuilderTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class LlamaServerArgumentBuilderTests
{
    private static AppConfig Config() => new()
    {
        ModelsDirectory = @"C:\models",
        Host = "127.0.0.1",
        Port = 8080
    };

    [Fact]
    public void Build_IncludesCoreArguments()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Equal(new[]
        {
            "-m", @"C:\models\model.gguf",
            "--host", "127.0.0.1",
            "--port", "8080",
            "-c", "8192",
            "-ngl", "0",
            "-ctk", "q8_0",
            "-ctv", "q8_0",
            "--alias", "model",
            "-b", "2048"
        }, args);
    }

    [Fact]
    public void Build_IncludesThreads_WhenSet()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.Threads = 8;

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Contains("--threads", args);
        Assert.Contains("8", args);
    }

    [Fact]
    public void Build_OmitsThreads_WhenNull()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.DoesNotContain("--threads", args);
    }

    [Fact]
    public void Build_IncludesFlashAttentionFlag_WhenEnabled()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.FlashAttention = true;

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Contains("--flash-attn", args);
    }

    [Fact]
    public void Build_OmitsFlashAttentionFlag_WhenDisabled()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.DoesNotContain("--flash-attn", args);
    }

    [Fact]
    public void Build_AppendsExtraArguments_SplitOnWhitespace()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.ExtraArguments = "--tensor-split 0.5,0.5";

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Equal("--tensor-split", args[^2]);
        Assert.Equal("0.5,0.5", args[^1]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LlamaServerArgumentBuilderTests`
Expected: build error, `LlamaServerArgumentBuilder` does not exist.

- [ ] **Step 3: Implement `LlamaServerArgumentBuilder.cs`**

```csharp
// src/LlamaCppLauncher/Server/LlamaServerArgumentBuilder.cs
using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Server;

public static class LlamaServerArgumentBuilder
{
    public static List<string> Build(AppConfig config, ModelProfile profile)
    {
        string modelPath = Path.Combine(config.ModelsDirectory, profile.FileName);

        var args = new List<string>
        {
            "-m", modelPath,
            "--host", config.Host,
            "--port", config.Port.ToString(),
            "-c", profile.CtxSize.ToString(),
            "-ngl", profile.NGpuLayers.ToString(),
            "-ctk", profile.KvCacheType,
            "-ctv", profile.KvCacheType,
            "--alias", profile.Alias,
            "-b", profile.BatchSize.ToString()
        };

        if (profile.Threads.HasValue)
        {
            args.Add("--threads");
            args.Add(profile.Threads.Value.ToString());
        }

        if (profile.FlashAttention)
        {
            args.Add("--flash-attn");
        }

        if (!string.IsNullOrWhiteSpace(profile.ExtraArguments))
        {
            args.AddRange(profile.ExtraArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return args;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LlamaServerArgumentBuilderTests`
Expected: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Server/LlamaServerArgumentBuilder.cs src/LlamaCppLauncher.Tests/Server/LlamaServerArgumentBuilderTests.cs
git commit -m "Add LlamaServerArgumentBuilder to build llama-server command-line arguments"
```

---

### Task 6: Port status infrastructure

**Files:**
- Create: `src/LlamaCppLauncher/Validation/PortStatus.cs`
- Create: `src/LlamaCppLauncher/Validation/PortStatusClassifier.cs`
- Create: `src/LlamaCppLauncher/Validation/ITcpPortProbe.cs`
- Create: `src/LlamaCppLauncher/Validation/TcpPortProbe.cs`
- Create: `src/LlamaCppLauncher/Validation/IPortChecker.cs`
- Create: `src/LlamaCppLauncher/Validation/PortCheckService.cs`
- Test: `src/LlamaCppLauncher.Tests/Validation/PortStatusClassifierTests.cs`
- Test: `src/LlamaCppLauncher.Tests/Validation/TcpPortProbeTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Validation/PortStatusClassifierTests.cs
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Validation;

public class PortStatusClassifierTests
{
    [Fact]
    public void Classify_ReturnsFree_WhenNotListening()
    {
        Assert.Equal(PortStatus.Free, PortStatusClassifier.Classify(isListening: false, isOwnedByLauncherProcess: false));
    }

    [Fact]
    public void Classify_ReturnsOccupiedByLauncher_WhenListeningAndOwnedByLauncher()
    {
        Assert.Equal(PortStatus.OccupiedByLauncher, PortStatusClassifier.Classify(isListening: true, isOwnedByLauncherProcess: true));
    }

    [Fact]
    public void Classify_ReturnsOccupiedByOther_WhenListeningAndNotOwnedByLauncher()
    {
        Assert.Equal(PortStatus.OccupiedByOther, PortStatusClassifier.Classify(isListening: true, isOwnedByLauncherProcess: false));
    }
}
```

```csharp
// src/LlamaCppLauncher.Tests/Validation/TcpPortProbeTests.cs
using System.Net;
using System.Net.Sockets;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Validation;

public class TcpPortProbeTests
{
    [Fact]
    public void IsPortListening_DetectsRealListeningSocket_AndFalseAfterStop()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var probe = new TcpPortProbe();

        try
        {
            Assert.True(probe.IsPortListening(port));
        }
        finally
        {
            listener.Stop();
        }

        Assert.False(probe.IsPortListening(port));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter "PortStatusClassifierTests|TcpPortProbeTests"`
Expected: build error, referenced types do not exist.

- [ ] **Step 3: Implement `PortStatus.cs`, `PortStatusClassifier.cs`, `ITcpPortProbe.cs`, `TcpPortProbe.cs`**

```csharp
// src/LlamaCppLauncher/Validation/PortStatus.cs
namespace LlamaCppLauncher.Validation;

public enum PortStatus
{
    Free,
    OccupiedByLauncher,
    OccupiedByOther
}
```

```csharp
// src/LlamaCppLauncher/Validation/PortStatusClassifier.cs
namespace LlamaCppLauncher.Validation;

public static class PortStatusClassifier
{
    public static PortStatus Classify(bool isListening, bool isOwnedByLauncherProcess)
    {
        if (!isListening)
        {
            return PortStatus.Free;
        }

        return isOwnedByLauncherProcess ? PortStatus.OccupiedByLauncher : PortStatus.OccupiedByOther;
    }
}
```

```csharp
// src/LlamaCppLauncher/Validation/ITcpPortProbe.cs
namespace LlamaCppLauncher.Validation;

public interface ITcpPortProbe
{
    bool IsPortListening(int port);
}
```

```csharp
// src/LlamaCppLauncher/Validation/TcpPortProbe.cs
using System.Net.NetworkInformation;

namespace LlamaCppLauncher.Validation;

public sealed class TcpPortProbe : ITcpPortProbe
{
    public bool IsPortListening(int port)
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        return properties.GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter "PortStatusClassifierTests|TcpPortProbeTests"`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Add `IPortChecker` and the concrete `PortCheckService` (no dedicated unit test — see note)**

```csharp
// src/LlamaCppLauncher/Validation/IPortChecker.cs
namespace LlamaCppLauncher.Validation;

public interface IPortChecker
{
    PortStatus GetStatus(int port);
}
```

```csharp
// src/LlamaCppLauncher/Validation/PortCheckService.cs
using System.Diagnostics;

namespace LlamaCppLauncher.Validation;

public sealed class PortCheckService : IPortChecker
{
    private readonly ITcpPortProbe _portProbe;
    private readonly string _launcherExecutablePath;

    public PortCheckService(ITcpPortProbe portProbe, string launcherExecutablePath)
    {
        _portProbe = portProbe;
        _launcherExecutablePath = launcherExecutablePath;
    }

    public PortStatus GetStatus(int port)
    {
        bool isListening = _portProbe.IsPortListening(port);
        bool isOwnedByLauncher = isListening && IsLauncherManagedProcessRunning();
        return PortStatusClassifier.Classify(isListening, isOwnedByLauncher);
    }

    private bool IsLauncherManagedProcessRunning()
    {
        if (string.IsNullOrEmpty(_launcherExecutablePath))
        {
            return false;
        }

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, _launcherExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Access denied reading MainModule for some processes (elevation/other user) — skip them.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
```

`PortCheckService` composes two already-tested pieces (`TcpPortProbe`, tested with a real socket in Step 4; `PortStatusClassifier`, tested with fakes in Step 4) with `Process.GetProcesses()` enumeration, which is an OS-level, per-machine-state operation that cannot be meaningfully unit tested in isolation (there's no way to spawn a real "launcher-managed" process in a hermetic test without starting a real executable). It is exercised by the manual smoke test in Task 23.

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/LlamaCppLauncher/Validation src/LlamaCppLauncher.Tests/Validation
git commit -m "Add port status classification, TCP port probing, and PortCheckService"
```

---

### Task 7: `ValidationService`

**Files:**
- Create: `src/LlamaCppLauncher/Validation/ValidationResult.cs`
- Create: `src/LlamaCppLauncher/Validation/ValidationService.cs`
- Create: `src/LlamaCppLauncher.Tests/TestSupport/StubPortChecker.cs`
- Test: `src/LlamaCppLauncher.Tests/Validation/ValidationServiceTests.cs`

- [ ] **Step 1: Add the shared `StubPortChecker` test double**

```csharp
// src/LlamaCppLauncher.Tests/TestSupport/StubPortChecker.cs
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.TestSupport;

public sealed class StubPortChecker : IPortChecker
{
    private readonly PortStatus _status;

    public StubPortChecker(PortStatus status)
    {
        _status = status;
    }

    public PortStatus GetStatus(int port) => _status;
}
```

- [ ] **Step 2: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Validation/ValidationServiceTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Tests.TestSupport;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Validation;

public class ValidationServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modelsDir;
    private readonly string _exePath;

    public ValidationServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        _modelsDir = Path.Combine(_tempDir, "models");
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "model.gguf"), "");
        _exePath = Path.Combine(_tempDir, "llama-server.exe");
        File.WriteAllText(_exePath, "");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private AppConfig ValidConfig() => new()
    {
        ExecutablePath = _exePath,
        ModelsDirectory = _modelsDir,
        Port = 8080
    };

    [Fact]
    public void ValidateGeneral_ReturnsValid_ForCorrectConfig()
    {
        var service = new ValidationService(new StubPortChecker(PortStatus.Free));

        ValidationResult result = service.ValidateGeneral(ValidConfig());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateGeneral_ReportsError_WhenExecutableMissing()
    {
        AppConfig config = ValidConfig();
        config.ExecutablePath = Path.Combine(_tempDir, "does-not-exist.exe");
        var service = new ValidationService(new StubPortChecker(PortStatus.Free));

        ValidationResult result = service.ValidateGeneral(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("executable not found"));
    }

    [Fact]
    public void ValidateGeneral_ReportsError_WhenModelsDirectoryHasNoGgufFiles()
    {
        string emptyDir = Path.Combine(_tempDir, "empty-models");
        Directory.CreateDirectory(emptyDir);
        AppConfig config = ValidConfig();
        config.ModelsDirectory = emptyDir;
        var service = new ValidationService(new StubPortChecker(PortStatus.Free));

        ValidationResult result = service.ValidateGeneral(config);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("No .gguf files found"));
    }

    [Fact]
    public void ValidateGeneral_ReportsError_WhenPortOccupiedByOtherProcess()
    {
        var service = new ValidationService(new StubPortChecker(PortStatus.OccupiedByOther));

        ValidationResult result = service.ValidateGeneral(ValidConfig());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("already in use"));
    }

    [Fact]
    public void ValidateGeneral_IsValid_WhenPortOccupiedByLauncherItself()
    {
        var service = new ValidationService(new StubPortChecker(PortStatus.OccupiedByLauncher));

        ValidationResult result = service.ValidateGeneral(ValidConfig());

        Assert.True(result.IsValid);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ValidationServiceTests`
Expected: build error, `ValidationService` / `ValidationResult` do not exist.

- [ ] **Step 4: Implement `ValidationResult.cs` and `ValidationService.cs`**

```csharp
// src/LlamaCppLauncher/Validation/ValidationResult.cs
namespace LlamaCppLauncher.Validation;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
}
```

```csharp
// src/LlamaCppLauncher/Validation/ValidationService.cs
using System.IO;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;

namespace LlamaCppLauncher.Validation;

public sealed class ValidationService
{
    private readonly IPortChecker _portChecker;

    public ValidationService(IPortChecker portChecker)
    {
        _portChecker = portChecker;
    }

    public ValidationResult ValidateGeneral(AppConfig config)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(config.ExecutablePath) || !File.Exists(config.ExecutablePath))
        {
            result.AddError($"llama.cpp executable not found at '{config.ExecutablePath}'.");
        }

        if (string.IsNullOrWhiteSpace(config.ModelsDirectory) || !Directory.Exists(config.ModelsDirectory))
        {
            result.AddError($"Models directory not found at '{config.ModelsDirectory}'.");
        }
        else if (!ModelDiscoveryService.DiscoverModelFiles(config.ModelsDirectory).Any())
        {
            result.AddError($"No .gguf files found in '{config.ModelsDirectory}'.");
        }

        PortStatus portStatus = _portChecker.GetStatus(config.Port);
        if (portStatus == PortStatus.OccupiedByOther)
        {
            result.AddError($"Port {config.Port} is already in use by another application.");
        }

        return result;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ValidationServiceTests`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 6: Commit**

```bash
git add src/LlamaCppLauncher/Validation/ValidationResult.cs src/LlamaCppLauncher/Validation/ValidationService.cs src/LlamaCppLauncher.Tests/Validation/ValidationServiceTests.cs src/LlamaCppLauncher.Tests/TestSupport
git commit -m "Add ValidationService covering executable, models directory, and port checks"
```

---

### Task 8: `LlamaServerApiClient` + `ParamFormatter`

**Files:**
- Create: `src/LlamaCppLauncher/Server/ModelInfo.cs`
- Create: `src/LlamaCppLauncher/Server/ParamFormatter.cs`
- Create: `src/LlamaCppLauncher/Server/LlamaServerApiClient.cs`
- Test: `src/LlamaCppLauncher.Tests/Server/ParamFormatterTests.cs`
- Test: `src/LlamaCppLauncher.Tests/Server/LlamaServerApiClientTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Server/ParamFormatterTests.cs
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class ParamFormatterTests
{
    [Theory]
    [InlineData(7_620_000_000, "7.62 B")]
    [InlineData(1_000_000_000, "1.00 B")]
    [InlineData(0, "0.00 B")]
    [InlineData(700_000_000, "0.70 B")]
    public void FormatTotalParams_FormatsAsBillionsWithTwoDecimals(long paramCount, string expected)
    {
        Assert.Equal(expected, ParamFormatter.FormatTotalParams(paramCount));
    }
}
```

```csharp
// src/LlamaCppLauncher.Tests/Server/LlamaServerApiClientTests.cs
using System.Net;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class LlamaServerApiClientTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode) { Content = new StringContent(_content) };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ParsesModelDetails()
    {
        const string json = """
        {
          "data": [
            { "id": "Qwen2.5-7B", "meta": { "n_ctx": 8192, "ftype": "Q4_K_M", "n_params": 7620000000 } }
          ]
        }
        """;
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.NotNull(info);
        Assert.Equal("Qwen2.5-7B", info!.Alias);
        Assert.Equal(8192, info.ContextSize);
        Assert.Equal("Q4_K_M", info.Quantization);
        Assert.Equal("7.62 B", info.TotalParams);
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ReturnsNull_WhenServerRespondsWithError()
    {
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "")));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.Null(info);
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ReturnsNull_WhenResponseHasNoModels()
    {
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, """{ "data": [] }""")));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.Null(info);
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_FallsBackToUnknown_WhenMetaIsMissing()
    {
        const string json = """
        {
          "data": [
            { "id": "Qwen2.5-7B" }
          ]
        }
        """;
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.NotNull(info);
        Assert.Equal("Qwen2.5-7B", info!.Alias);
        Assert.Equal(0, info.ContextSize);
        Assert.Equal("Unknown", info.Quantization);
        Assert.Equal("Unknown", info.TotalParams);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter "ParamFormatterTests|LlamaServerApiClientTests"`
Expected: build error, referenced types do not exist.

- [ ] **Step 3: Implement `ModelInfo.cs`, `ParamFormatter.cs`, `LlamaServerApiClient.cs`**

```csharp
// src/LlamaCppLauncher/Server/ModelInfo.cs
using System.Text.Json.Serialization;

namespace LlamaCppLauncher.Server;

public sealed class ModelsApiResponse
{
    [JsonPropertyName("data")]
    public List<ModelsApiEntry> Data { get; set; } = new();
}

public sealed class ModelsApiEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("meta")]
    public ModelsApiMeta? Meta { get; set; }
}

public sealed class ModelsApiMeta
{
    [JsonPropertyName("n_ctx")]
    public int NCtx { get; set; }

    [JsonPropertyName("ftype")]
    public string? Ftype { get; set; }

    [JsonPropertyName("n_params")]
    public long NParams { get; set; }
}

public sealed record RunningModelInfo(string Alias, int ContextSize, string Quantization, string TotalParams);
```

```csharp
// src/LlamaCppLauncher/Server/ParamFormatter.cs
using System.Globalization;

namespace LlamaCppLauncher.Server;

public static class ParamFormatter
{
    public static string FormatTotalParams(long paramCount)
    {
        double billions = paramCount / 1_000_000_000.0;
        return billions.ToString("0.00", CultureInfo.InvariantCulture) + " B";
    }
}
```

```csharp
// src/LlamaCppLauncher/Server/LlamaServerApiClient.cs
using System.Net.Http;
using System.Text.Json;

namespace LlamaCppLauncher.Server;

public sealed class LlamaServerApiClient
{
    private readonly HttpClient _httpClient;

    public LlamaServerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RunningModelInfo?> GetRunningModelInfoAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        string url = $"http://{host}:{port}/v1/models";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ModelsApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ModelsApiResponse>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (parsed is null || parsed.Data.Count == 0)
        {
            return null;
        }

        ModelsApiEntry entry = parsed.Data[0];
        return new RunningModelInfo(
            Alias: entry.Id,
            ContextSize: entry.Meta?.NCtx ?? 0,
            Quantization: entry.Meta?.Ftype ?? "Unknown",
            TotalParams: entry.Meta is null ? "Unknown" : ParamFormatter.FormatTotalParams(entry.Meta.NParams));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter "ParamFormatterTests|LlamaServerApiClientTests"`
Expected: `Passed! - Failed: 0, Passed: 8`.

`GetRunningModelInfoAsync` now also catches `TaskCanceledException` (an `HttpClient`-internal timeout — the default `HttpClient.Timeout` is 100 seconds, and a slow/hung `llama-server` would otherwise throw this uncaught) and `JsonException` (a malformed or unexpectedly-shaped response body), alongside the original `HttpRequestException`. This matters because Task 18's `TrayController.BuildAboutViewModel` calls this method synchronously via `.GetAwaiter().GetResult()` — an uncaught exception there would surface as an unhandled exception when the user opens the About window, not a graceful "no info available" state. The `when (!cancellationToken.IsCancellationRequested)` guard on the `TaskCanceledException` catch specifically distinguishes "the caller asked us to cancel" (which should still propagate as a real cancellation) from "the HTTP call itself timed out" (which should degrade to `null` like every other failure mode here).

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Server/ModelInfo.cs src/LlamaCppLauncher/Server/ParamFormatter.cs src/LlamaCppLauncher/Server/LlamaServerApiClient.cs src/LlamaCppLauncher.Tests/Server/ParamFormatterTests.cs src/LlamaCppLauncher.Tests/Server/LlamaServerApiClientTests.cs
git commit -m "Add LlamaServerApiClient to query /v1/models and format running-model info"
```

---

### Task 9: `AutoStartService`

**Files:**
- Create: `src/LlamaCppLauncher/AutoStart/IRunKeyStore.cs`
- Create: `src/LlamaCppLauncher/AutoStart/RegistryRunKeyStore.cs`
- Create: `src/LlamaCppLauncher/AutoStart/AutoStartService.cs`
- Test: `src/LlamaCppLauncher.Tests/AutoStart/AutoStartServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/AutoStart/AutoStartServiceTests.cs
using LlamaCppLauncher.AutoStart;

namespace LlamaCppLauncher.Tests.AutoStart;

public class AutoStartServiceTests
{
    private sealed class InMemoryRunKeyStore : IRunKeyStore
    {
        private readonly Dictionary<string, string> _values = new();

        public void SetValue(string name, string value) => _values[name] = value;
        public void RemoveValue(string name) => _values.Remove(name);
        public bool TryGetValue(string name, out string? value) => _values.TryGetValue(name, out value);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenValueNotSet()
    {
        var service = new AutoStartService(new InMemoryRunKeyStore());

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_WritesQuotedExecutablePath()
    {
        var store = new InMemoryRunKeyStore();
        var service = new AutoStartService(store);

        service.SetEnabled(true, @"C:\LlamaCppLauncher\LlamaCppLauncher.exe");

        Assert.True(service.IsEnabled());
        store.TryGetValue("LlamaCppLauncher", out string? value);
        Assert.Equal("\"C:\\LlamaCppLauncher\\LlamaCppLauncher.exe\"", value);
    }

    [Fact]
    public void SetEnabled_False_RemovesValue()
    {
        var store = new InMemoryRunKeyStore();
        var service = new AutoStartService(store);
        service.SetEnabled(true, @"C:\app.exe");

        service.SetEnabled(false, @"C:\app.exe");

        Assert.False(service.IsEnabled());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter AutoStartServiceTests`
Expected: build error, `IRunKeyStore` / `AutoStartService` do not exist.

- [ ] **Step 3: Implement `IRunKeyStore.cs` and `AutoStartService.cs`**

```csharp
// src/LlamaCppLauncher/AutoStart/IRunKeyStore.cs
namespace LlamaCppLauncher.AutoStart;

public interface IRunKeyStore
{
    void SetValue(string name, string value);
    void RemoveValue(string name);
    bool TryGetValue(string name, out string? value);
}
```

```csharp
// src/LlamaCppLauncher/AutoStart/AutoStartService.cs
namespace LlamaCppLauncher.AutoStart;

public sealed class AutoStartService
{
    private const string ValueName = "LlamaCppLauncher";
    private readonly IRunKeyStore _store;

    public AutoStartService(IRunKeyStore store)
    {
        _store = store;
    }

    public bool IsEnabled() => _store.TryGetValue(ValueName, out _);

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (enabled)
        {
            _store.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            _store.RemoveValue(ValueName);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter AutoStartServiceTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 5: Add the concrete `RegistryRunKeyStore` (no dedicated unit test — see note)**

```csharp
// src/LlamaCppLauncher/AutoStart/RegistryRunKeyStore.cs
using Microsoft.Win32;

namespace LlamaCppLauncher.AutoStart;

public sealed class RegistryRunKeyStore : IRunKeyStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(name, value);
    }

    public void RemoveValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public bool TryGetValue(string name, out string? value)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        value = key?.GetValue(name) as string;
        return value is not null;
    }
}
```

`RegistryRunKeyStore` is a thin wrapper over the real Windows registry — writing to `HKEY_CURRENT_USER` from an automated test would mutate the developer machine's real autostart configuration, which is exactly the kind of external side effect that should not run unattended. `AutoStartService`'s actual logic is already fully covered against the in-memory fake in Step 1; `RegistryRunKeyStore` itself is exercised by the manual smoke test in Task 23 (toggle "Start with Windows" in Settings, then verify with `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v LlamaCppLauncher`).

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/LlamaCppLauncher/AutoStart src/LlamaCppLauncher.Tests/AutoStart
git commit -m "Add AutoStartService and registry-backed Run key store"
```

---

### Task 10: `TrayMenuStateBuilder`

**Files:**
- Create: `src/LlamaCppLauncher/Tray/ServiceState.cs`
- Create: `src/LlamaCppLauncher/Tray/TrayMenuState.cs`
- Create: `src/LlamaCppLauncher/Tray/TrayMenuStateBuilder.cs`
- Test: `src/LlamaCppLauncher.Tests/Tray/TrayMenuStateBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Tray/TrayMenuStateBuilderTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Tray;

namespace LlamaCppLauncher.Tests.Tray;

public class TrayMenuStateBuilderTests
{
    private static ModelProfile Model(string fileName, string alias) => new() { FileName = fileName, Alias = alias };

    [Fact]
    public void Build_DisablesSwitch_WhenServiceOff()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "A") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.Off, models, runningFileName: null);

        Assert.False(state.SwitchEnabled);
    }

    [Fact]
    public void Build_EnablesSwitch_AndMarksRunningModelAsCurrent_WhenServiceOn()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "Alpha"), Model("b.gguf", "Beta") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.On, models, runningFileName: "b.gguf");

        Assert.True(state.SwitchEnabled);
        Assert.Equal(2, state.SwitchEntries.Count);
        SwitchMenuEntry currentEntry = Assert.Single(state.SwitchEntries, e => e.IsCurrent);
        Assert.Equal("b.gguf", currentEntry.FileName);
    }

    [Fact]
    public void Build_SortsEntriesByAliasCaseInsensitive()
    {
        var models = new List<ModelProfile> { Model("z.gguf", "zeta"), Model("a.gguf", "Alpha") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.On, models, runningFileName: null);

        Assert.Equal("Alpha", state.SwitchEntries[0].Alias);
        Assert.Equal("zeta", state.SwitchEntries[1].Alias);
    }

    [Fact]
    public void Build_NoEntryIsCurrent_WhenServiceOff_EvenIfRunningFileNameMatches()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "A") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.Off, models, runningFileName: "a.gguf");

        Assert.DoesNotContain(state.SwitchEntries, e => e.IsCurrent);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter TrayMenuStateBuilderTests`
Expected: build error, referenced types do not exist.

- [ ] **Step 3: Implement `ServiceState.cs`, `TrayMenuState.cs`, `TrayMenuStateBuilder.cs`**

```csharp
// src/LlamaCppLauncher/Tray/ServiceState.cs
namespace LlamaCppLauncher.Tray;

public enum ServiceState
{
    Off,
    On
}
```

```csharp
// src/LlamaCppLauncher/Tray/TrayMenuState.cs
namespace LlamaCppLauncher.Tray;

public sealed class SwitchMenuEntry
{
    public required string Alias { get; init; }
    public required string FileName { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class TrayMenuState
{
    public ServiceState ServiceState { get; init; }
    public bool SwitchEnabled { get; init; }
    public IReadOnlyList<SwitchMenuEntry> SwitchEntries { get; init; } = Array.Empty<SwitchMenuEntry>();
}
```

```csharp
// src/LlamaCppLauncher/Tray/TrayMenuStateBuilder.cs
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Tray;

public static class TrayMenuStateBuilder
{
    public static TrayMenuState Build(ServiceState state, IReadOnlyList<ModelProfile> models, string? runningFileName)
    {
        List<SwitchMenuEntry> entries = models
            .OrderBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(m => new SwitchMenuEntry
            {
                Alias = m.Alias,
                FileName = m.FileName,
                IsCurrent = state == ServiceState.On &&
                    string.Equals(m.FileName, runningFileName, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        return new TrayMenuState
        {
            ServiceState = state,
            SwitchEnabled = state == ServiceState.On,
            SwitchEntries = entries
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter TrayMenuStateBuilderTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Tray/ServiceState.cs src/LlamaCppLauncher/Tray/TrayMenuState.cs src/LlamaCppLauncher/Tray/TrayMenuStateBuilder.cs src/LlamaCppLauncher.Tests/Tray
git commit -m "Add TrayMenuStateBuilder for tray menu Service/Switch state"
```

---

### Task 11: `LauncherBootstrapper`

**Files:**
- Create: `src/LlamaCppLauncher/Startup/StartupAction.cs`
- Create: `src/LlamaCppLauncher/Startup/StartupDecision.cs`
- Create: `src/LlamaCppLauncher/Startup/LauncherBootstrapper.cs`
- Test: `src/LlamaCppLauncher.Tests/Startup/LauncherBootstrapperTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Startup/LauncherBootstrapperTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Tests.TestSupport;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Startup;

public class LauncherBootstrapperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;
    private readonly string _modelsDir;
    private readonly string _exePath;

    public LauncherBootstrapperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        _modelsDir = Path.Combine(_tempDir, "models");
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "model.gguf"), "");
        _exePath = Path.Combine(_tempDir, "llama-server.exe");
        File.WriteAllText(_exePath, "");
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private LauncherBootstrapper CreateBootstrapper(PortStatus portStatus = PortStatus.Free)
    {
        return new LauncherBootstrapper(
            new ConfigService(_configPath),
            new ValidationService(new StubPortChecker(portStatus)));
    }

    [Fact]
    public void Decide_OpensSettingsWithError_WhenConfigFileMissing()
    {
        StartupDecision decision = CreateBootstrapper().Decide();

        Assert.Equal(StartupAction.OpenSettingsWithError, decision.Action);
    }

    [Fact]
    public void Decide_OpensSettingsWithError_WhenConfigFileCorrupted()
    {
        File.WriteAllText(_configPath, "{ not valid json");

        StartupDecision decision = CreateBootstrapper().Decide();

        Assert.Equal(StartupAction.OpenSettingsWithError, decision.Action);
    }

    [Fact]
    public void Decide_StaysOffWithNotification_WhenValidationFails()
    {
        var config = new AppConfig { ExecutablePath = "missing.exe", ModelsDirectory = _modelsDir, Port = 8080 };
        new ConfigService(_configPath).Save(config);

        StartupDecision decision = CreateBootstrapper().Decide();

        Assert.Equal(StartupAction.StayOffWithNotification, decision.Action);
        // This config also has no default model configured (Models is empty), so this assertion
        // pins that validation errors take precedence over the "no default model" message.
        Assert.Contains("executable not found", decision.Message);
    }

    [Fact]
    public void Decide_StaysOffWithNotification_WhenNoDefaultModelConfigured()
    {
        var config = new AppConfig
        {
            ExecutablePath = _exePath,
            ModelsDirectory = _modelsDir,
            Port = 8080,
            Models = { ModelProfile.CreateDefault("model.gguf") }
        };
        new ConfigService(_configPath).Save(config);

        StartupDecision decision = CreateBootstrapper().Decide();

        Assert.Equal(StartupAction.StayOffWithNotification, decision.Action);
        Assert.Contains("No default model", decision.Message);
    }

    [Fact]
    public void Decide_AutoStartsDefaultModel_WhenConfigIsValid()
    {
        var defaultModel = ModelProfile.CreateDefault("model.gguf");
        defaultModel.IsDefault = true;
        var config = new AppConfig
        {
            ExecutablePath = _exePath,
            ModelsDirectory = _modelsDir,
            Port = 8080,
            Models = { defaultModel }
        };
        new ConfigService(_configPath).Save(config);

        StartupDecision decision = CreateBootstrapper().Decide();

        Assert.Equal(StartupAction.AutoStartDefaultModel, decision.Action);
        Assert.Equal("model.gguf", decision.DefaultModel!.FileName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LauncherBootstrapperTests`
Expected: build error, referenced types do not exist.

- [ ] **Step 3: Implement `StartupAction.cs`, `StartupDecision.cs`, `LauncherBootstrapper.cs`**

```csharp
// src/LlamaCppLauncher/Startup/StartupAction.cs
namespace LlamaCppLauncher.Startup;

public enum StartupAction
{
    AutoStartDefaultModel,
    OpenSettingsWithError,
    StayOffWithNotification
}
```

```csharp
// src/LlamaCppLauncher/Startup/StartupDecision.cs
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Startup;

public sealed class StartupDecision
{
    public required StartupAction Action { get; init; }
    public string? Message { get; init; }
    public AppConfig? Config { get; init; }
    public ModelProfile? DefaultModel { get; init; }
}
```

```csharp
// src/LlamaCppLauncher/Startup/LauncherBootstrapper.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Startup;

public sealed class LauncherBootstrapper
{
    private readonly ConfigService _configService;
    private readonly ValidationService _validationService;

    public LauncherBootstrapper(ConfigService configService, ValidationService validationService)
    {
        _configService = configService;
        _validationService = validationService;
    }

    public StartupDecision Decide()
    {
        AppConfig? config;
        try
        {
            config = _configService.Load();
        }
        catch (ConfigLoadException)
        {
            return new StartupDecision
            {
                Action = StartupAction.OpenSettingsWithError,
                Message = "Configuration file is corrupted. Please review your settings."
            };
        }

        if (config is null)
        {
            return new StartupDecision
            {
                Action = StartupAction.OpenSettingsWithError,
                Message = "Welcome! Please configure llama.cpp before starting."
            };
        }

        ValidationResult validation = _validationService.ValidateGeneral(config);
        if (!validation.IsValid)
        {
            return new StartupDecision
            {
                Action = StartupAction.StayOffWithNotification,
                Message = string.Join(" ", validation.Errors),
                Config = config
            };
        }

        ModelProfile? defaultModel = config.Models.FirstOrDefault(m => m.IsDefault);
        if (defaultModel is null)
        {
            return new StartupDecision
            {
                Action = StartupAction.StayOffWithNotification,
                Message = "No default model is configured. Open Settings to choose one.",
                Config = config
            };
        }

        return new StartupDecision
        {
            Action = StartupAction.AutoStartDefaultModel,
            Config = config,
            DefaultModel = defaultModel
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LauncherBootstrapperTests`
Expected: `Passed! - Failed: 0, Passed: 5`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Startup src/LlamaCppLauncher.Tests/Startup
git commit -m "Add LauncherBootstrapper implementing the SPEC startup decision matrix"
```

---

### Task 12: `LlamaServerProcessManager`

**Files:**
- Create: `src/LlamaCppLauncher/Server/LlamaServerProcessManager.cs`
- Test: `src/LlamaCppLauncher.Tests/Server/LlamaServerProcessManagerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Server/LlamaServerProcessManagerTests.cs
using System.ComponentModel;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class LlamaServerProcessManagerTests : IDisposable
{
    private readonly string _tempDir;

    public LlamaServerProcessManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Start_Throws_WhenExecutableDoesNotExist()
    {
        var manager = new LlamaServerProcessManager(
            Path.Combine(_tempDir, "out.log"),
            Path.Combine(_tempDir, "err.log"));
        var config = new AppConfig
        {
            ExecutablePath = Path.Combine(_tempDir, "does-not-exist.exe"),
            ModelsDirectory = _tempDir,
            Port = 8080
        };

        Assert.ThrowsAny<Win32Exception>(() => manager.Start(config, ModelProfile.CreateDefault("model.gguf")));
        Assert.False(manager.IsRunning);
    }

    [Fact]
    public void Stop_IsNoOp_WhenNothingIsRunning()
    {
        var manager = new LlamaServerProcessManager(
            Path.Combine(_tempDir, "out.log"),
            Path.Combine(_tempDir, "err.log"));

        manager.Stop();

        Assert.False(manager.IsRunning);
    }

    [Fact]
    public void Start_Throws_WhenAlreadyRunning()
    {
        // Starting twice without stopping should be rejected before ever touching Process.Start
        // for a real executable — exercised here using the same "missing executable" path so the
        // test stays hermetic; the first Start() attempt fails, so IsRunning is still false and this
        // documents the intended guard rather than exercising it end-to-end (see Task 23 for that).
        var manager = new LlamaServerProcessManager(
            Path.Combine(_tempDir, "out.log"),
            Path.Combine(_tempDir, "err.log"));
        var config = new AppConfig { ExecutablePath = Path.Combine(_tempDir, "missing.exe"), ModelsDirectory = _tempDir, Port = 8080 };

        Assert.ThrowsAny<Win32Exception>(() => manager.Start(config, ModelProfile.CreateDefault("model.gguf")));
        Assert.False(manager.IsRunning);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LlamaServerProcessManagerTests`
Expected: build error, `LlamaServerProcessManager` does not exist.

- [ ] **Step 3: Implement `LlamaServerProcessManager.cs`**

```csharp
// src/LlamaCppLauncher/Server/LlamaServerProcessManager.cs
using System.Diagnostics;
using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Server;

public sealed class LlamaServerProcessManager : IDisposable
{
    private readonly string _outLogPath;
    private readonly string _errLogPath;
    private Process? _process;

    public event EventHandler? ServerExited;

    public bool IsRunning => _process is { HasExited: false };
    public ModelProfile? RunningModel { get; private set; }

    public LlamaServerProcessManager(string outLogPath, string errLogPath)
    {
        _outLogPath = outLogPath;
        _errLogPath = errLogPath;
    }

    public void Start(AppConfig config, ModelProfile profile)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A server process is already running. Stop it before starting another.");
        }

        string? logDirectory = Path.GetDirectoryName(_outLogPath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(config.ExecutablePath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string arg in LlamaServerArgumentBuilder.Build(config, profile))
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += OnProcessExited;

        process.Start();

        var outWriter = new StreamWriter(_outLogPath, append: false) { AutoFlush = true };
        var errWriter = new StreamWriter(_errLogPath, append: false) { AutoFlush = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outWriter.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) errWriter.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _process = process;
        RunningModel = profile;
    }

    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process.Exited -= OnProcessExited;
        _process.Dispose();
        _process = null;
        RunningModel = null;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        RunningModel = null;
        ServerExited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter LlamaServerProcessManagerTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

Note: `Start()`/`Stop()`'s success path against a real, listening `llama-server.exe` cannot be exercised here — there is no such binary or GPU/CPU-capable model in this environment. That path (process starts, binds the port, `ServerExited` fires on a real crash, `Stop()` terminates a real process) is covered by the manual smoke test in Task 23.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Server/LlamaServerProcessManager.cs src/LlamaCppLauncher.Tests/Server/LlamaServerProcessManagerTests.cs
git commit -m "Add LlamaServerProcessManager to start/stop llama-server and track its lifecycle"
```

---

### Task 13: `GeneralSettingsViewModel`

**Files:**
- Create: `src/LlamaCppLauncher/Settings/GeneralSettingsViewModel.cs`
- Test: `src/LlamaCppLauncher.Tests/Settings/GeneralSettingsViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Settings/GeneralSettingsViewModelTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Settings;
using LlamaCppLauncher.Tests.TestSupport;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Settings;

public class GeneralSettingsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _exePath;
    private readonly string _modelsDir;

    public GeneralSettingsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        _modelsDir = Path.Combine(_tempDir, "models");
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "model.gguf"), "");
        _exePath = Path.Combine(_tempDir, "llama-server.exe");
        File.WriteAllText(_exePath, "");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void RevalidateExecutablePath_SetsError_WhenFileMissing()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ExecutablePath = Path.Combine(_tempDir, "missing.exe");

        Assert.Equal("File not found.", viewModel.ExecutablePathError);
    }

    [Fact]
    public void RevalidateExecutablePath_ClearsError_WhenFileExists()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ExecutablePath = _exePath;

        Assert.Null(viewModel.ExecutablePathError);
    }

    [Fact]
    public void RevalidateModelsDirectory_ReportsModelCount()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ModelsDirectory = _modelsDir;

        Assert.Equal("Found 1 model.", viewModel.ModelsDirectoryStatus);
    }

    [Fact]
    public void RevalidatePort_FlagsError_WhenOccupiedByOtherProcess()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.OccupiedByOther));

        viewModel.Port = 9999;

        Assert.True(viewModel.PortHasError);
    }

    [Fact]
    public void RevalidatePort_NoError_WhenFree()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.Port = 9999;

        Assert.False(viewModel.PortHasError);
    }

    [Fact]
    public void FromConfig_PopulatesAndValidatesAllFieldsImmediately()
    {
        var config = new AppConfig { ExecutablePath = _exePath, ModelsDirectory = _modelsDir, Port = 8080, StartWithWindows = true };

        var viewModel = GeneralSettingsViewModel.FromConfig(config, new StubPortChecker(PortStatus.Free));

        Assert.Equal(_exePath, viewModel.ExecutablePath);
        Assert.Null(viewModel.ExecutablePathError);
        Assert.Equal("Found 1 model.", viewModel.ModelsDirectoryStatus);
        Assert.True(viewModel.StartWithWindows);
    }

    [Fact]
    public void ApplyTo_CopiesFieldsIntoConfig()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free))
        {
            ExecutablePath = _exePath,
            ModelsDirectory = _modelsDir,
            Port = 9090,
            StartWithWindows = true
        };
        var config = new AppConfig();

        viewModel.ApplyTo(config);

        Assert.Equal(_exePath, config.ExecutablePath);
        Assert.Equal(_modelsDir, config.ModelsDirectory);
        Assert.Equal(9090, config.Port);
        Assert.True(config.StartWithWindows);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter GeneralSettingsViewModelTests`
Expected: build error, `GeneralSettingsViewModel` does not exist.

- [ ] **Step 3: Implement `GeneralSettingsViewModel.cs`**

```csharp
// src/LlamaCppLauncher/Settings/GeneralSettingsViewModel.cs
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;
using LlamaCppLauncher.Validation;
using PortStatus = LlamaCppLauncher.Validation.PortStatus;

namespace LlamaCppLauncher.Settings;

public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly IPortChecker _portChecker;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _modelsDirectory = string.Empty;

    [ObservableProperty]
    private int _port = 8080;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string? _executablePathError;

    [ObservableProperty]
    private string? _modelsDirectoryStatus;

    [ObservableProperty]
    private string? _portStatusText;

    [ObservableProperty]
    private bool _portHasError;

    public GeneralSettingsViewModel(IPortChecker portChecker)
    {
        _portChecker = portChecker;
    }

    public static GeneralSettingsViewModel FromConfig(AppConfig config, IPortChecker portChecker)
    {
        var viewModel = new GeneralSettingsViewModel(portChecker)
        {
            ExecutablePath = config.ExecutablePath,
            ModelsDirectory = config.ModelsDirectory,
            Port = config.Port,
            StartWithWindows = config.StartWithWindows
        };
        viewModel.RevalidateAll();
        return viewModel;
    }

    partial void OnExecutablePathChanged(string value) => RevalidateExecutablePath();
    partial void OnModelsDirectoryChanged(string value) => RevalidateModelsDirectory();
    partial void OnPortChanged(int value) => RevalidatePort();

    public void RevalidateAll()
    {
        RevalidateExecutablePath();
        RevalidateModelsDirectory();
        RevalidatePort();
    }

    private void RevalidateExecutablePath()
    {
        ExecutablePathError = File.Exists(ExecutablePath) ? null : "File not found.";
    }

    private void RevalidateModelsDirectory()
    {
        if (!Directory.Exists(ModelsDirectory))
        {
            ModelsDirectoryStatus = "Directory not found.";
            return;
        }

        int count = ModelDiscoveryService.DiscoverModelFiles(ModelsDirectory).Count;
        ModelsDirectoryStatus = count == 0
            ? "No .gguf files found."
            : $"Found {count} model{(count == 1 ? "" : "s")}.";
    }

    private void RevalidatePort()
    {
        PortStatus status = _portChecker.GetStatus(Port);
        switch (status)
        {
            case PortStatus.Free:
                PortStatusText = "Port is available.";
                PortHasError = false;
                break;
            case PortStatus.OccupiedByLauncher:
                PortStatusText = "In use by this launcher's server.";
                PortHasError = false;
                break;
            default:
                PortStatusText = "Port is already in use by another application.";
                PortHasError = true;
                break;
        }
    }

    public void ApplyTo(AppConfig config)
    {
        config.ExecutablePath = ExecutablePath;
        config.ModelsDirectory = ModelsDirectory;
        config.Port = Port;
        config.StartWithWindows = StartWithWindows;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter GeneralSettingsViewModelTests`
Expected: `Passed! - Failed: 0, Passed: 7`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Settings/GeneralSettingsViewModel.cs src/LlamaCppLauncher.Tests/Settings/GeneralSettingsViewModelTests.cs
git commit -m "Add GeneralSettingsViewModel with real-time validation"
```

---

### Task 14: `ModelsSettingsViewModel`

**Files:**
- Create: `src/LlamaCppLauncher/Settings/ModelsSettingsViewModel.cs`
- Test: `src/LlamaCppLauncher.Tests/Settings/ModelsSettingsViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Settings/ModelsSettingsViewModelTests.cs
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Settings;

namespace LlamaCppLauncher.Tests.Settings;

public class ModelsSettingsViewModelTests
{
    private static ModelProfile Model(string fileName, bool isDefault = false)
    {
        ModelProfile profile = ModelProfile.CreateDefault(fileName);
        profile.IsDefault = isDefault;
        return profile;
    }

    [Fact]
    public void Constructor_SelectsFirstModelByDefault()
    {
        ModelProfile modelA = Model("a.gguf");
        ModelProfile modelB = Model("b.gguf");

        var viewModel = new ModelsSettingsViewModel(new[] { modelA, modelB });

        Assert.Same(modelA, viewModel.SelectedModel);
    }

    [Fact]
    public void SetSelectedModelAsDefault_ClearsFlagOnOtherModels()
    {
        ModelProfile modelA = Model("a.gguf", isDefault: true);
        ModelProfile modelB = Model("b.gguf");
        var viewModel = new ModelsSettingsViewModel(new[] { modelA, modelB }) { SelectedModel = modelB };

        viewModel.SetSelectedModelAsDefault();

        Assert.False(modelA.IsDefault);
        Assert.True(modelB.IsDefault);
    }

    [Fact]
    public void ApplyTo_WritesModelsListToConfig()
    {
        ModelProfile modelA = Model("a.gguf");
        var viewModel = new ModelsSettingsViewModel(new[] { modelA });
        var config = new AppConfig();

        viewModel.ApplyTo(config);

        Assert.Single(config.Models);
        Assert.Equal("a.gguf", config.Models[0].FileName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelsSettingsViewModelTests`
Expected: build error, `ModelsSettingsViewModel` does not exist.

- [ ] **Step 3: Implement `ModelsSettingsViewModel.cs`**

```csharp
// src/LlamaCppLauncher/Settings/ModelsSettingsViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Settings;

public sealed partial class ModelsSettingsViewModel : ObservableObject
{
    public ObservableCollection<ModelProfile> Models { get; }

    [ObservableProperty]
    private ModelProfile? _selectedModel;

    public ModelsSettingsViewModel(IEnumerable<ModelProfile> models)
    {
        Models = new ObservableCollection<ModelProfile>(models);
        SelectedModel = Models.FirstOrDefault();
    }

    [RelayCommand]
    public void SetSelectedModelAsDefault()
    {
        if (SelectedModel is null)
        {
            return;
        }

        foreach (ModelProfile model in Models)
        {
            model.IsDefault = ReferenceEquals(model, SelectedModel);
        }

        // ModelProfile is a plain data class (no INotifyPropertyChanged) so bound fields like
        // "is this the default model" won't refresh on their own — force one now.
        OnPropertyChanged(nameof(SelectedModel));
    }

    public void ApplyTo(AppConfig config)
    {
        config.Models = Models.ToList();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter ModelsSettingsViewModelTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Settings/ModelsSettingsViewModel.cs src/LlamaCppLauncher.Tests/Settings/ModelsSettingsViewModelTests.cs
git commit -m "Add ModelsSettingsViewModel with exclusive Default Model selection"
```

**Scope note on SPEC.md §4.2's "prompts the user before discarding changes":** this plan's `SaveCommand` (Task 15) always saves the *entire* in-memory `Models` collection at once, not a per-model draft. Because every `ModelProfile` in the `ComboBox` is edited in place (two-way bound directly to the live object), switching the dropdown selection never discards anything — the previous model's edits stay in memory and are written out whenever Save is next clicked, regardless of which model is currently selected. The only way to actually lose edits is to close the Settings window without ever clicking Save, which is the same standard "unsaved changes" risk every settings dialog has and isn't singled out for a special confirmation dialog in this plan. This is a deliberate simplification (YAGNI: no draft/staging copy, no discard-confirmation dialog) rather than an oversight.

---

### Task 15: `SettingsViewModel`

**Files:**
- Create: `src/LlamaCppLauncher/Settings/SettingsViewModel.cs`
- Test: `src/LlamaCppLauncher.Tests/Settings/SettingsViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/Settings/SettingsViewModelTests.cs
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Settings;
using LlamaCppLauncher.Tests.TestSupport;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Settings;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public SettingsViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private sealed class InMemoryRunKeyStore : IRunKeyStore
    {
        public bool Enabled;
        public void SetValue(string name, string value) => Enabled = true;
        public void RemoveValue(string name) => Enabled = false;
        public bool TryGetValue(string name, out string? value)
        {
            value = Enabled ? "\"path\"" : null;
            return Enabled;
        }
    }

    [Fact]
    public void Save_PersistsGeneralAndModelSettings_AndSyncsAutoStart()
    {
        var config = new AppConfig();
        var general = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free))
        {
            ExecutablePath = "exe.exe",
            ModelsDirectory = "models",
            Port = 9090,
            StartWithWindows = true
        };
        var models = new ModelsSettingsViewModel(new[] { ModelProfile.CreateDefault("m.gguf") });
        var runKeyStore = new InMemoryRunKeyStore();
        var viewModel = new SettingsViewModel(
            config, general, models, new ConfigService(_configPath), new AutoStartService(runKeyStore));

        viewModel.SaveCommand.Execute(null);

        AppConfig? saved = new ConfigService(_configPath).Load();
        Assert.NotNull(saved);
        Assert.Equal("exe.exe", saved!.ExecutablePath);
        Assert.Equal(9090, saved.Port);
        Assert.Single(saved.Models);
        Assert.True(runKeyStore.Enabled);
    }

    [Fact]
    public void ShowGeneralPage_And_ShowModelsPage_ToggleSelection()
    {
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)),
            new ModelsSettingsViewModel(Array.Empty<ModelProfile>()),
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        viewModel.ShowModelsPageCommand.Execute(null);
        Assert.False(viewModel.IsGeneralPageSelected);
        Assert.True(viewModel.IsModelsPageSelected);

        viewModel.ShowGeneralPageCommand.Execute(null);
        Assert.True(viewModel.IsGeneralPageSelected);
        Assert.False(viewModel.IsModelsPageSelected);
    }

    [Fact]
    public void Save_SetsSaveNotice_WhenSavedModelsIncludeTheCurrentlyRunningOne()
    {
        var models = new ModelsSettingsViewModel(new[] { ModelProfile.CreateDefault("running.gguf") });
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)) { ExecutablePath = "exe.exe", ModelsDirectory = "models" },
            models,
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()),
            runningModelFileName: "running.gguf");

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Changes will apply the next time this model is started.", viewModel.SaveNotice);
    }

    [Fact]
    public void Save_LeavesSaveNoticeNull_WhenNoModelIsCurrentlyRunning()
    {
        var models = new ModelsSettingsViewModel(new[] { ModelProfile.CreateDefault("a.gguf") });
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)) { ExecutablePath = "exe.exe", ModelsDirectory = "models" },
            models,
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()),
            runningModelFileName: null);

        viewModel.SaveCommand.Execute(null);

        Assert.Null(viewModel.SaveNotice);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter SettingsViewModelTests`
Expected: build error, `SettingsViewModel` does not exist.

- [ ] **Step 3: Implement `SettingsViewModel.cs`**

```csharp
// src/LlamaCppLauncher/Settings/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly AutoStartService _autoStartService;
    private readonly AppConfig _config;
    private readonly string? _runningModelFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelsPageSelected))]
    private bool _isGeneralPageSelected = true;

    public bool IsModelsPageSelected => !IsGeneralPageSelected;

    [ObservableProperty]
    private string? _saveNotice;

    public GeneralSettingsViewModel General { get; }
    public ModelsSettingsViewModel Models { get; }

    public SettingsViewModel(
        AppConfig config,
        GeneralSettingsViewModel general,
        ModelsSettingsViewModel models,
        ConfigService configService,
        AutoStartService autoStartService,
        string? runningModelFileName = null)
    {
        _config = config;
        General = general;
        Models = models;
        _configService = configService;
        _autoStartService = autoStartService;
        _runningModelFileName = runningModelFileName;
    }

    [RelayCommand]
    private void ShowGeneralPage() => IsGeneralPageSelected = true;

    [RelayCommand]
    private void ShowModelsPage() => IsGeneralPageSelected = false;

    [RelayCommand]
    private void Save()
    {
        General.ApplyTo(_config);
        Models.ApplyTo(_config);
        _configService.Save(_config);
        _autoStartService.SetEnabled(_config.StartWithWindows, Environment.ProcessPath ?? string.Empty);

        // SPEC.md §4.3: saving params for the currently-running model does not auto-restart the
        // server — surface that explicitly instead of silently doing nothing.
        SaveNotice = _runningModelFileName is not null && Models.Models.Any(m => m.FileName == _runningModelFileName)
            ? "Changes will apply the next time this model is started."
            : null;
    }

    [RelayCommand]
    private void BrowseExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "llama-server executable (*.exe)|*.exe",
            FileName = General.ExecutablePath
        };
        if (dialog.ShowDialog() == true)
        {
            General.ExecutablePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseModelsDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { FolderName = General.ModelsDirectory };
        if (dialog.ShowDialog() == true)
        {
            General.ModelsDirectory = dialog.FolderName;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter SettingsViewModelTests`
Expected: `Passed! - Failed: 0, Passed: 4`.

`BrowseExecutable`/`BrowseModelsDirectory` open real OS file/folder pickers (`Microsoft.Win32.OpenFileDialog`/`OpenFolderDialog`, both confirmed present in `net8.0-windows` during planning) and are not covered by the unit tests above — they're exercised in the Task 23 manual smoke test.

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/Settings/SettingsViewModel.cs src/LlamaCppLauncher.Tests/Settings/SettingsViewModelTests.cs
git commit -m "Add SettingsViewModel tying together General/Models pages, save, and browse dialogs"
```

---

### Task 16: `AboutViewModel`

**Files:**
- Create: `src/LlamaCppLauncher/About/AboutViewModel.cs`
- Test: `src/LlamaCppLauncher.Tests/About/AboutViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// src/LlamaCppLauncher.Tests/About/AboutViewModelTests.cs
using LlamaCppLauncher.About;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.About;

public class AboutViewModelTests
{
    [Fact]
    public void NotRunning_SetsIsServiceRunningFalse()
    {
        AboutViewModel viewModel = AboutViewModel.NotRunning();

        Assert.False(viewModel.IsServiceRunning);
    }

    [Fact]
    public void FromRunningModel_PopulatesAllFieldsFromModelInfo()
    {
        var info = new RunningModelInfo("Qwen2.5-7B", 8192, "Q4_K_M", "7.62 B");

        AboutViewModel viewModel = AboutViewModel.FromRunningModel("Qwen2.5-7B-Instruct-Q4_K_M.gguf", "127.0.0.1", 8080, info);

        Assert.True(viewModel.IsServiceRunning);
        Assert.Equal("Qwen2.5-7B-Instruct-Q4_K_M.gguf", viewModel.FileName);
        Assert.Equal("Qwen2.5-7B", viewModel.Alias);
        Assert.Equal("http://127.0.0.1:8080/", viewModel.WebChatUrl);
        Assert.Equal("http://127.0.0.1:8080/v1", viewModel.ApiUrl);
        Assert.Equal("Q4_K_M", viewModel.Quantization);
        Assert.Equal("7.62 B", viewModel.TotalParams);
        Assert.Equal("8192", viewModel.ContextSize);
    }

    [Fact]
    public void ProjectHomepageUrl_PointsToLlamaCppRepository()
    {
        AboutViewModel viewModel = AboutViewModel.NotRunning();

        Assert.Equal("https://github.com/ggml-org/llama.cpp", viewModel.ProjectHomepageUrl);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail to compile**

Run: `dotnet test src/LlamaCppLauncher.sln --filter AboutViewModelTests`
Expected: build error, `AboutViewModel` does not exist.

- [ ] **Step 3: Implement `AboutViewModel.cs`**

```csharp
// src/LlamaCppLauncher/About/AboutViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.About;

public sealed partial class AboutViewModel : ObservableObject
{
    private const string ProjectUrl = "https://github.com/ggml-org/llama.cpp";

    [ObservableProperty]
    private bool _isServiceRunning;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    private string _webChatUrl = string.Empty;

    [ObservableProperty]
    private string _apiUrl = string.Empty;

    [ObservableProperty]
    private string _quantization = string.Empty;

    [ObservableProperty]
    private string _totalParams = string.Empty;

    [ObservableProperty]
    private string _contextSize = string.Empty;

    public string ProjectHomepageUrl => ProjectUrl;

    public static AboutViewModel NotRunning() => new() { IsServiceRunning = false };

    public static AboutViewModel FromRunningModel(string fileName, string host, int port, RunningModelInfo info)
    {
        return new AboutViewModel
        {
            IsServiceRunning = true,
            FileName = fileName,
            Alias = info.Alias,
            WebChatUrl = $"http://{host}:{port}/",
            ApiUrl = $"http://{host}:{port}/v1",
            Quantization = info.Quantization,
            TotalParams = info.TotalParams,
            ContextSize = info.ContextSize.ToString()
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/LlamaCppLauncher.sln --filter AboutViewModelTests`
Expected: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Step 5: Run the full test suite before moving to the UI layer**

Run: `dotnet test src/LlamaCppLauncher.sln`
Expected: all tests from Tasks 2–16 pass (roughly 60 tests), 0 failures. This is the last purely-logic task — Tasks 17 onward build the WPF/WinForms UI on top of everything tested so far.

- [ ] **Step 6: Commit**

```bash
git add src/LlamaCppLauncher/About/AboutViewModel.cs src/LlamaCppLauncher.Tests/About/AboutViewModelTests.cs
git commit -m "Add AboutViewModel for running-model display and project homepage link"
```

---

### Task 17: `SingleInstanceService`

**Files:**
- Create: `src/LlamaCppLauncher/Tray/SingleInstanceService.cs`

No dedicated unit test: a named `Mutex` only proves single-instance behavior across *separate OS processes*, which means a real test would have to launch a second copy of the actual executable — that's an integration scenario, exercised in the Task 23 manual smoke test (launch the built `.exe` twice and confirm only one tray icon appears).

- [ ] **Step 1: Implement `SingleInstanceService.cs`**

```csharp
// src/LlamaCppLauncher/Tray/SingleInstanceService.cs
namespace LlamaCppLauncher.Tray;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstanceService(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out bool createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/LlamaCppLauncher/Tray/SingleInstanceService.cs
git commit -m "Add SingleInstanceService using a named Mutex"
```

---

### Task 18: `TrayController` (tray icon, context menu, balloon notifications)

**Files:**
- Create: `src/LlamaCppLauncher/Tray/TrayController.cs`

This class composes services already fully unit-tested in Tasks 3, 6, 7, 8, 10, 12 (`ConfigService`, `TcpPortProbe`, `ValidationService`, `LlamaServerApiClient`, `TrayMenuStateBuilder`, `LlamaServerProcessManager`) with `System.Windows.Forms.NotifyIcon`/`ContextMenuStrip`, which cannot be driven headlessly in this environment (there is no real system tray to assert against in a unit test). It is verified by `dotnet build` here and by the manual smoke test in Task 23.

Per SPEC.md §6, starting a model must confirm the server actually bound the port within ~10 seconds before declaring success (matching the original bat script's post-launch check) — this means the tray menu's click handlers have to be `async` so the up-to-10-second wait doesn't freeze the tray's message loop.

- [ ] **Step 1: Implement `TrayController.cs`**

```csharp
// src/LlamaCppLauncher/Tray/TrayController.cs
using System.IO;
using LlamaCppLauncher.About;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Validation;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LlamaCppLauncher.Tray;

public sealed class TrayController : IDisposable
{
    private static readonly TimeSpan PortBindTimeout = TimeSpan.FromSeconds(10);

    private readonly ConfigService _configService;
    private readonly ValidationService _validationService;
    private readonly ITcpPortProbe _portProbe;
    private readonly LlamaServerProcessManager _processManager;
    private readonly LlamaServerApiClient _apiClient;
    private readonly Drawing.Icon _colorIcon;
    private readonly Drawing.Icon _bwIcon;
    private readonly Forms.NotifyIcon _notifyIcon;

    private AppConfig _config = new();

    public event EventHandler? SettingsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? ExitRequested;

    public AppConfig CurrentConfig => _config;

    public string? RunningModelFileName => _processManager.IsRunning ? _processManager.RunningModel?.FileName : null;

    public TrayController(
        ConfigService configService,
        ValidationService validationService,
        ITcpPortProbe portProbe,
        LlamaServerProcessManager processManager,
        LlamaServerApiClient apiClient)
    {
        _configService = configService;
        _validationService = validationService;
        _portProbe = portProbe;
        _processManager = processManager;
        _apiClient = apiClient;

        _colorIcon = LoadIcon("cpp_logo_color.ico");
        _bwIcon = LoadIcon("cpp_logo_bw.ico");

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _bwIcon,
            Text = "llama.cpp Launcher — Stopped",
            Visible = true
        };

        _processManager.ServerExited += (_, _) =>
        {
            ShowBalloon("llama.cpp Launcher", "The server process exited unexpectedly. Check the error log for details.", isError: true);
            RefreshMenu();
        };
    }

    private static Drawing.Icon LoadIcon(string fileName)
    {
        using Stream stream = typeof(TrayController).Assembly
            .GetManifestResourceStream($"LlamaCppLauncher.Assets.{fileName}")!;
        return new Drawing.Icon(stream);
    }

    public async Task ApplyStartupDecisionAsync(StartupDecision decision)
    {
        if (decision.Config is not null)
        {
            _config = decision.Config;
        }

        if (decision.Action == StartupAction.AutoStartDefaultModel && decision.DefaultModel is not null)
        {
            await StartServerAsync(decision.DefaultModel);
        }
        else if (decision.Message is not null && decision.Action == StartupAction.StayOffWithNotification)
        {
            ShowBalloon("llama.cpp Launcher", decision.Message, isError: true);
        }

        RefreshMenu();
    }

    public async Task ToggleServiceAsync()
    {
        if (_processManager.IsRunning)
        {
            StopServer();
        }
        else
        {
            ValidationResult validation = _validationService.ValidateGeneral(_config);
            if (!validation.IsValid)
            {
                ShowBalloon("llama.cpp Launcher", string.Join(" ", validation.Errors), isError: true);
            }
            else
            {
                ModelProfile? defaultModel = _config.Models.FirstOrDefault(m => m.IsDefault);
                if (defaultModel is null)
                {
                    ShowBalloon("llama.cpp Launcher", "No default model is configured. Open Settings to choose one.", isError: true);
                }
                else
                {
                    await StartServerAsync(defaultModel);
                }
            }
        }

        RefreshMenu();
    }

    public async Task SwitchToAsync(string fileName)
    {
        ModelProfile? target = _config.Models.FirstOrDefault(m =>
            string.Equals(m.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        StopServer();
        await StartServerAsync(target);
        RefreshMenu();
    }

    public void ReloadConfigAfterSettingsSaved()
    {
        _config = _configService.Load() ?? _config;
        RefreshMenu();
    }

    public AboutViewModel BuildAboutViewModel()
    {
        if (!_processManager.IsRunning || _processManager.RunningModel is null)
        {
            return AboutViewModel.NotRunning();
        }

        RunningModelInfo? info = _apiClient
            .GetRunningModelInfoAsync(_config.Host, _config.Port)
            .GetAwaiter()
            .GetResult();

        return info is null
            ? AboutViewModel.NotRunning()
            : AboutViewModel.FromRunningModel(_processManager.RunningModel.FileName, _config.Host, _config.Port, info);
    }

    private async Task StartServerAsync(ModelProfile profile)
    {
        try
        {
            _processManager.Start(_config, profile);
        }
        catch (Exception ex)
        {
            ShowBalloon("llama.cpp Launcher", $"Failed to start the server: {ex.Message}", isError: true);
            return;
        }

        if (!await WaitForPortToBindAsync())
        {
            StopServer();
            ShowBalloon(
                "llama.cpp Launcher",
                $"The server did not start listening on port {_config.Port} within {PortBindTimeout.TotalSeconds:0} seconds. Check the error log for details.",
                isError: true);
            return;
        }

        _config.LastRunningModelFileName = profile.FileName;
    }

    private async Task<bool> WaitForPortToBindAsync()
    {
        DateTime deadline = DateTime.UtcNow + PortBindTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_portProbe.IsPortListening(_config.Port))
            {
                return true;
            }
            await Task.Delay(250);
        }
        return _portProbe.IsPortListening(_config.Port);
    }

    private void StopServer() => _processManager.Stop();

    private void ShowBalloon(string title, string message, bool isError)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(5000, title, message, isError ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
    }

    private void RefreshMenu()
    {
        bool isRunning = _processManager.IsRunning;

        _notifyIcon.Icon = isRunning ? _colorIcon : _bwIcon;
        _notifyIcon.Text = Truncate(isRunning && _processManager.RunningModel is not null
            ? $"llama.cpp Launcher — Running ({_processManager.RunningModel.Alias})"
            : "llama.cpp Launcher — Stopped");

        ServiceState state = isRunning ? ServiceState.On : ServiceState.Off;
        TrayMenuState menuState = TrayMenuStateBuilder.Build(state, _config.Models, _config.LastRunningModelFileName);

        var menu = new Forms.ContextMenuStrip();

        var serviceItem = new Forms.ToolStripMenuItem(isRunning ? "Service: On" : "Service: Off");
        serviceItem.Click += async (_, _) => await ToggleServiceAsync();
        menu.Items.Add(serviceItem);

        var settingsItem = new Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var switchItem = new Forms.ToolStripMenuItem("Switch") { Enabled = menuState.SwitchEnabled };
        foreach (SwitchMenuEntry entry in menuState.SwitchEntries)
        {
            var entryItem = new Forms.ToolStripMenuItem(entry.IsCurrent ? $"\u25CF {entry.Alias}" : entry.Alias)
            {
                Font = entry.IsCurrent
                    ? new Drawing.Font(Forms.Control.DefaultFont, Drawing.FontStyle.Bold)
                    : Forms.Control.DefaultFont
            };
            entryItem.Click += async (_, _) => await SwitchToAsync(entry.FileName);
            switchItem.DropDownItems.Add(entryItem);
        }
        menu.Items.Add(switchItem);

        var aboutItem = new Forms.ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(aboutItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        Forms.ContextMenuStrip? previousMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        previousMenu?.Dispose();
    }

    private static string Truncate(string text) => text.Length <= 63 ? text : text[..63];

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _colorIcon.Dispose();
        _bwIcon.Dispose();
    }
}
```

`NotifyIcon.Text` is capped at 63 characters by the underlying Win32 API — `Truncate` avoids an `ArgumentException` for a long alias/tooltip. The `async (_, _) => await ...` click handlers are `async void` event handlers, the standard (if imperfect) WinForms/WPF pattern for firing off async work from a UI event — every awaited call already wraps its own failure paths in a `try`/`catch` that shows a balloon instead of letting an exception escape.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/LlamaCppLauncher/Tray/TrayController.cs
git commit -m "Add TrayController wiring the tray icon, context menu, and balloon notifications"
```

---

### Task 19: `SettingsWindow`

**Files:**
- Create: `src/LlamaCppLauncher/Settings/SettingsWindow.xaml`
- Create: `src/LlamaCppLauncher/Settings/SettingsWindow.xaml.cs`

XAML binding paths are not checked by `dotnet build` (only compiled markup element/attribute names are) — this task's `dotnet build` step confirms the file compiles; the manual smoke test in Task 23 confirms the bindings actually work at runtime.

- [ ] **Step 1: Create `SettingsWindow.xaml`**

```xml
<!-- src/LlamaCppLauncher/Settings/SettingsWindow.xaml -->
<ui:FluentWindow
  x:Class="LlamaCppLauncher.Settings.SettingsWindow"
  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
  Title="Settings — llama.cpp Launcher"
  Width="640"
  Height="560"
  ExtendsContentIntoTitleBar="True"
  WindowBackdropType="Mica"
  WindowCornerPreference="Round"
  WindowStartupLocation="CenterScreen"
  >
  <Window.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVis" />
  </Window.Resources>
  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <ui:TitleBar Grid.Row="0" Title="Settings" />

    <Grid Grid.Row="1">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="180" />
        <ColumnDefinition Width="*" />
      </Grid.ColumnDefinitions>

      <StackPanel Grid.Column="0" Background="{DynamicResource ControlFillColorDefaultBrush}">
        <ui:Button Margin="8" HorizontalContentAlignment="Left" Content="General" Command="{Binding ShowGeneralPageCommand}" />
        <ui:Button Margin="8,0,8,8" HorizontalContentAlignment="Left" Content="Models" Command="{Binding ShowModelsPageCommand}" />
      </StackPanel>

      <StackPanel Grid.Column="1" Margin="16" Visibility="{Binding IsGeneralPageSelected, Converter={StaticResource BoolToVis}}">
        <TextBlock Margin="0,0,0,4" Text="llama.cpp executable path" />
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
          </Grid.ColumnDefinitions>
          <ui:TextBox Grid.Column="0" Text="{Binding General.ExecutablePath, UpdateSourceTrigger=PropertyChanged}" />
          <ui:Button Grid.Column="1" Margin="8,0,0,0" Content="Browse…" Command="{Binding BrowseExecutableCommand}" />
        </Grid>
        <TextBlock Foreground="{DynamicResource SystemFillColorCriticalBrush}" Text="{Binding General.ExecutablePathError}" />

        <TextBlock Margin="0,16,0,4" Text="Models directory" />
        <Grid>
          <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
          </Grid.ColumnDefinitions>
          <ui:TextBox Grid.Column="0" Text="{Binding General.ModelsDirectory, UpdateSourceTrigger=PropertyChanged}" />
          <ui:Button Grid.Column="1" Margin="8,0,0,0" Content="Browse…" Command="{Binding BrowseModelsDirectoryCommand}" />
        </Grid>
        <TextBlock Text="{Binding General.ModelsDirectoryStatus}" />

        <TextBlock Margin="0,16,0,4" Text="Port" />
        <ui:NumberBox Width="120" HorizontalAlignment="Left" Value="{Binding General.Port, UpdateSourceTrigger=PropertyChanged}" />
        <TextBlock Text="{Binding General.PortStatusText}">
          <TextBlock.Style>
            <Style TargetType="TextBlock">
              <Setter Property="Foreground" Value="{DynamicResource TextFillColorPrimaryBrush}" />
              <Style.Triggers>
                <DataTrigger Binding="{Binding General.PortHasError}" Value="True">
                  <Setter Property="Foreground" Value="{DynamicResource SystemFillColorCriticalBrush}" />
                </DataTrigger>
              </Style.Triggers>
            </Style>
          </TextBlock.Style>
        </TextBlock>

        <CheckBox Margin="0,16,0,0" Content="Start with Windows" IsChecked="{Binding General.StartWithWindows}" />

        <ui:Button Margin="0,24,0,0" HorizontalAlignment="Left" Appearance="Primary" Content="Save" Command="{Binding SaveCommand}" />
        <TextBlock Margin="0,8,0,0" Foreground="{DynamicResource SystemFillColorSuccessBrush}" Text="{Binding SaveNotice}" />
      </StackPanel>

      <Grid Grid.Column="1" Margin="16" Visibility="{Binding IsModelsPageSelected, Converter={StaticResource BoolToVis}}">
        <Grid.RowDefinitions>
          <RowDefinition Height="Auto" />
          <RowDefinition Height="*" />
          <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ComboBox Grid.Row="0" Margin="0,0,0,16" DisplayMemberPath="Alias" ItemsSource="{Binding Models.Models}" SelectedItem="{Binding Models.SelectedModel}" />

        <StackPanel Grid.Row="1">
          <TextBlock Text="Alias" />
          <ui:TextBox Margin="0,0,0,12" Text="{Binding Models.SelectedModel.Alias, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Text="CTX_SIZE" />
          <ui:NumberBox Margin="0,0,0,12" Value="{Binding Models.SelectedModel.CtxSize, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Text="N_GPU_LAYERS" />
          <ui:NumberBox Margin="0,0,0,12" Value="{Binding Models.SelectedModel.NGpuLayers, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Text="KV cache quantization (-ctk/-ctv)" />
          <ComboBox Margin="0,0,0,12" SelectedValuePath="Content" SelectedValue="{Binding Models.SelectedModel.KvCacheType}">
            <ComboBoxItem Content="q8_0" />
            <ComboBoxItem Content="f16" />
            <ComboBoxItem Content="q4_0" />
          </ComboBox>

          <TextBlock Text="Threads (--threads)" />
          <ui:TextBox Margin="0,0,0,12" PlaceholderText="Auto" Text="{Binding Models.SelectedModel.Threads, UpdateSourceTrigger=PropertyChanged}" />

          <TextBlock Text="Batch size (-b)" />
          <ui:NumberBox Margin="0,0,0,12" Value="{Binding Models.SelectedModel.BatchSize, UpdateSourceTrigger=PropertyChanged}" />

          <CheckBox Margin="0,0,0,12" Content="Enable Flash Attention (--flash-attn)" IsChecked="{Binding Models.SelectedModel.FlashAttention}" />

          <TextBlock Margin="0,0,0,4" FontWeight="SemiBold" Foreground="{DynamicResource SystemFillColorSuccessBrush}" Text="This is the Default Model" Visibility="{Binding Models.SelectedModel.IsDefault, Converter={StaticResource BoolToVis}}" />
          <ui:Button Margin="0,0,0,12" HorizontalAlignment="Left" Content="Set as Default Model" Command="{Binding Models.SetSelectedModelAsDefaultCommand}" />

          <TextBlock Text="Extra command-line arguments" />
          <ui:TextBox Text="{Binding Models.SelectedModel.ExtraArguments, UpdateSourceTrigger=PropertyChanged}" />
        </StackPanel>

        <ui:Button Grid.Row="2" Margin="0,16,0,0" HorizontalAlignment="Left" Appearance="Primary" Content="Save this model's configuration" Command="{Binding SaveCommand}" />
        <TextBlock Grid.Row="2" Margin="0,0,0,40" HorizontalAlignment="Right" Foreground="{DynamicResource SystemFillColorSuccessBrush}" Text="{Binding SaveNotice}" />
      </Grid>
    </Grid>
  </Grid>
</ui:FluentWindow>
```

Both pages share the same `SaveCommand`/`SaveNotice` — this plan's Settings window always saves the whole `AppConfig` (General fields + every model in the `Models` collection) in one shot rather than a separate "save just this page" action, so either Save button has the same effect (see the scope note at the end of Task 14). `SaveNotice` (Task 15) is only non-null when the model that was just saved is the one currently running, per SPEC.md §4.3.

- [ ] **Step 2: Create `SettingsWindow.xaml.cs`**

```csharp
// src/LlamaCppLauncher/Settings/SettingsWindow.xaml.cs
using Wpf.Ui.Controls;

namespace LlamaCppLauncher.Settings;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/LlamaCppLauncher/Settings/SettingsWindow.xaml src/LlamaCppLauncher/Settings/SettingsWindow.xaml.cs
git commit -m "Add SettingsWindow with General and Models pages"
```

---

### Task 20: `AboutWindow`

**Files:**
- Create: `src/LlamaCppLauncher/About/InverseBooleanToVisibilityConverter.cs`
- Create: `src/LlamaCppLauncher/About/AboutWindow.xaml`
- Create: `src/LlamaCppLauncher/About/AboutWindow.xaml.cs`

- [ ] **Step 1: Create `InverseBooleanToVisibilityConverter.cs`**

```csharp
// src/LlamaCppLauncher/About/InverseBooleanToVisibilityConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace LlamaCppLauncher.About;

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
```

- [ ] **Step 2: Create `AboutWindow.xaml`**

```xml
<!-- src/LlamaCppLauncher/About/AboutWindow.xaml -->
<ui:FluentWindow
  x:Class="LlamaCppLauncher.About.AboutWindow"
  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:local="clr-namespace:LlamaCppLauncher.About"
  xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
  Title="About — llama.cpp Launcher"
  Width="420"
  Height="440"
  ExtendsContentIntoTitleBar="True"
  ResizeMode="NoResize"
  WindowBackdropType="Mica"
  WindowCornerPreference="Round"
  WindowStartupLocation="CenterScreen"
  >
  <Window.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVis" />
    <local:InverseBooleanToVisibilityConverter x:Key="InverseBoolToVis" />
  </Window.Resources>
  <Grid Margin="16">
    <Grid.RowDefinitions>
      <RowDefinition Height="Auto" />
      <RowDefinition Height="*" />
      <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>

    <ui:TitleBar Grid.Row="0" Title="About" />

    <StackPanel Grid.Row="1" Margin="0,12,0,0" Visibility="{Binding IsServiceRunning, Converter={StaticResource BoolToVis}}">
      <TextBlock FontWeight="SemiBold" Text="File name" />
      <TextBlock Margin="0,0,0,8" Text="{Binding FileName}" />

      <TextBlock FontWeight="SemiBold" Text="Alias" />
      <TextBlock Margin="0,0,0,8" Text="{Binding Alias}" />

      <TextBlock FontWeight="SemiBold" Text="Web Chat URL" />
      <TextBlock Margin="0,0,0,8" Text="{Binding WebChatUrl}" />

      <TextBlock FontWeight="SemiBold" Text="API URL" />
      <TextBlock Margin="0,0,0,8" Text="{Binding ApiUrl}" />

      <TextBlock FontWeight="SemiBold" Text="Quantization" />
      <TextBlock Margin="0,0,0,8" Text="{Binding Quantization}" />

      <TextBlock FontWeight="SemiBold" Text="Total Params" />
      <TextBlock Margin="0,0,0,8" Text="{Binding TotalParams}" />

      <TextBlock FontWeight="SemiBold" Text="Context Size" />
      <TextBlock Margin="0,0,0,8" Text="{Binding ContextSize}" />
    </StackPanel>

    <TextBlock Grid.Row="1" Margin="0,12,0,0" Text="No model is currently running." Visibility="{Binding IsServiceRunning, Converter={StaticResource InverseBoolToVis}}" />

    <Button Grid.Row="2" Margin="0,16,0,0" HorizontalAlignment="Left" Background="Transparent" BorderThickness="0" Cursor="Hand" Click="OnProjectHomepageClick">
      <StackPanel Orientation="Horizontal">
        <Image Width="24" Height="24" Source="pack://application:,,,/Assets/llamacpp.png" />
        <TextBlock Margin="8,0,0,0" VerticalAlignment="Center" Text="llama.cpp on GitHub" />
      </StackPanel>
    </Button>
  </Grid>
</ui:FluentWindow>
```

- [ ] **Step 3: Create `AboutWindow.xaml.cs`**

```csharp
// src/LlamaCppLauncher/About/AboutWindow.xaml.cs
using System.Diagnostics;
using System.Windows;
using Wpf.Ui.Controls;

namespace LlamaCppLauncher.About;

public partial class AboutWindow : FluentWindow
{
    public AboutWindow(AboutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnProjectHomepageClick(object sender, RoutedEventArgs e)
    {
        var viewModel = (AboutViewModel)DataContext;
        Process.Start(new ProcessStartInfo(viewModel.ProjectHomepageUrl) { UseShellExecute = true });
    }
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/About/InverseBooleanToVisibilityConverter.cs src/LlamaCppLauncher/About/AboutWindow.xaml src/LlamaCppLauncher/About/AboutWindow.xaml.cs
git commit -m "Add AboutWindow with running-model details and llama.cpp project link"
```

---

### Task 21: `App.xaml` / `App.xaml.cs` composition root

**Files:**
- Modify: `src/LlamaCppLauncher/App.xaml`
- Modify: `src/LlamaCppLauncher/App.xaml.cs`

- [ ] **Step 1: Replace `App.xaml`**

```xml
<!-- src/LlamaCppLauncher/App.xaml -->
<Application
  x:Class="LlamaCppLauncher.App"
  xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
  ShutdownMode="OnExplicitShutdown"
  >
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ui:ThemesDictionary Theme="Dark" />
        <ui:ControlsDictionary />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

No `StartupUri` — the app has no main window; `ShutdownMode="OnExplicitShutdown"` is required so that closing the transient Settings/About windows does not terminate the whole tray app (confirmed necessary and working during planning).

- [ ] **Step 2: Replace `App.xaml.cs`**

```csharp
// src/LlamaCppLauncher/App.xaml.cs
using System.IO;
using System.Net.Http;
using System.Windows;
using LlamaCppLauncher.About;
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;
using LlamaCppLauncher.Server;
using LlamaCppLauncher.Settings;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Tray;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher;

public partial class App : System.Windows.Application
{
    private static readonly string AppDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaCppLauncher");

    private SingleInstanceService? _singleInstance;
    private TrayController? _trayController;
    private HttpClient? _httpClient;
    private ConfigService? _configService;
    private AutoStartService? _autoStartService;
    private LlamaServerProcessManager? _processManager;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService("LlamaCppLauncher_SingleInstance_Mutex");
        if (!_singleInstance.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        Directory.CreateDirectory(AppDataDirectory);
        string configPath = Path.Combine(AppDataDirectory, "config.json");
        string logDirectory = Path.Combine(AppDataDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        _configService = new ConfigService(configPath);
        var portProbe = new TcpPortProbe();
        var portChecker = new PortCheckService(portProbe, GetConfiguredExecutablePath(_configService));
        var validationService = new ValidationService(portChecker);
        _processManager = new LlamaServerProcessManager(
            Path.Combine(logDirectory, "llama-server.out.log"),
            Path.Combine(logDirectory, "llama-server.err.log"));
        _autoStartService = new AutoStartService(new RegistryRunKeyStore());
        _httpClient = new HttpClient();
        var apiClient = new LlamaServerApiClient(_httpClient);

        _trayController = new TrayController(_configService, validationService, portProbe, _processManager, apiClient);
        _trayController.SettingsRequested += (_, _) => OpenSettings();
        _trayController.AboutRequested += (_, _) => OpenAbout();
        _trayController.ExitRequested += (_, _) => ExitApplication();

        var bootstrapper = new LauncherBootstrapper(_configService, validationService);
        StartupDecision decision = bootstrapper.Decide();
        await _trayController.ApplyStartupDecisionAsync(decision);

        if (decision.Action == StartupAction.OpenSettingsWithError)
        {
            OpenSettings();
        }
    }

    private static string GetConfiguredExecutablePath(ConfigService configService)
    {
        try
        {
            return configService.Load()?.ExecutablePath ?? string.Empty;
        }
        catch (ConfigLoadException)
        {
            return string.Empty;
        }
    }

    private void OpenSettings()
    {
        AppConfig config = _trayController!.CurrentConfig;
        IReadOnlyList<string> discovered = ModelDiscoveryService.DiscoverModelFiles(config.ModelsDirectory);
        List<ModelProfile> mergedModels = ModelDiscoveryService.MergeWithConfiguredModels(discovered, config.Models);

        var portChecker = new PortCheckService(new TcpPortProbe(), config.ExecutablePath);
        GeneralSettingsViewModel generalViewModel = GeneralSettingsViewModel.FromConfig(config, portChecker);
        var modelsViewModel = new ModelsSettingsViewModel(mergedModels);
        var settingsViewModel = new SettingsViewModel(
            config, generalViewModel, modelsViewModel, _configService!, _autoStartService!, _trayController!.RunningModelFileName);

        var window = new SettingsWindow(settingsViewModel);
        window.Closed += (_, _) => _trayController!.ReloadConfigAfterSettingsSaved();
        window.Show();
    }

    private void OpenAbout()
    {
        var window = new AboutWindow(_trayController!.BuildAboutViewModel());
        window.Show();
    }

    private void ExitApplication()
    {
        _processManager?.Stop();
        _trayController?.Dispose();
        _singleInstance?.Dispose();
        _httpClient?.Dispose();
        Shutdown();
    }
}
```

`OnStartup` is declared `async void` — that's the standard (and only) way to `await` something during WPF startup, since the base `Application.OnStartup(StartupEventArgs)` signature returns `void` and can't be changed to `Task` in an override. It's safe here because everything awaited inside (`TrayController.ApplyStartupDecisionAsync`) already catches its own exceptions internally (see Task 18) rather than letting them propagate out unhandled.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build src/LlamaCppLauncher.sln`
Expected: `Build succeeded.`

- [ ] **Step 4: Run the full test suite one more time**

Run: `dotnet test src/LlamaCppLauncher.sln`
Expected: all tests still pass (App.xaml.cs has no dedicated tests — it is pure composition of already-tested pieces).

- [ ] **Step 5: Commit**

```bash
git add src/LlamaCppLauncher/App.xaml src/LlamaCppLauncher/App.xaml.cs
git commit -m "Wire up App.xaml.cs composition root: single instance, startup decision, tray, Settings, About"
```

---

### Task 22: Packaging and README

**Files:**
- Create: `src/README.md`
- Modify: `.gitignore` (repo root)

- [ ] **Step 1: Add build artifacts to `.gitignore`**

Append to the repo-root `.gitignore` (already contains `.superpowers/` from the SPEC.md work):

```
**/bin/
**/obj/
src/**/publish/
```

- [ ] **Step 2: Create `src/README.md`**

```markdown
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
installed. On first launch (no `config.json` yet in
`%LOCALAPPDATA%\LlamaCppLauncher`), it opens the Settings window automatically —
point "llama.cpp executable path" at your `llama-server.exe`, "Models directory"
at a folder of `.gguf` files, pick a Default Model on the Models page, and Save.
```

- [ ] **Step 3: Run the publish command to verify it succeeds**

```bash
cd src
dotnet publish LlamaCppLauncher/LlamaCppLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Expected: publish succeeds and `src/publish/LlamaCppLauncher.exe` exists.

- [ ] **Step 4: Commit**

```bash
git add .gitignore src/README.md
git commit -m "Add build/run/publish README and ignore build output directories"
```

(Do not commit the `src/publish/` output itself — it's covered by the `.gitignore` entry added in Step 1.)

---

### Task 23: Manual end-to-end smoke test

This is not automated — it requires a real `llama-server.exe` binary and at least two real `.gguf` model files, neither of which exist in this repository or in the environment this plan was written in. Run through this checklist on a real Windows machine with llama.cpp built and a couple of small `.gguf` models available, using the `publish/LlamaCppLauncher.exe` from Task 22.

- [ ] **First run**: delete `%LOCALAPPDATA%\LlamaCppLauncher` if present, launch the exe. Expected: tray icon appears black-and-white, Settings window opens automatically with an error/welcome banner.
- [ ] **Configure**: in Settings → General, set the executable path and models directory to real paths; confirm the inline validation icons/text update in real time (valid path → no error; invalid path → "File not found."; models directory with 0 `.gguf` → "No .gguf files found."). Set a port already in use by another program (e.g. start a `python -m http.server 8080`) and confirm the port row shows the "already in use by another application" error.
- [ ] **Configure models**: switch to port 8080's actual availability (stop the python server), go to Settings → Models, pick a model in the dropdown, set Alias/CTX_SIZE/etc., click "Set as Default Model", confirm "This is the Default Model" appears, click Save.
- [ ] **Restart the launcher**: close and relaunch the exe. Expected: tray icon turns color automatically (auto-started the Default Model), tooltip shows `Running (<alias>)`.
- [ ] **About while running**: right-click → About. Expected: File name, Alias, Web Chat URL, API URL, Quantization, Total Params, Context Size are all populated and correct; clicking the llama.cpp logo/link opens the GitHub repo in the default browser.
- [ ] **Switch**: configure a second model as non-default in Settings, reopen the tray menu → Switch. Expected: submenu lists both models, the running one is bold/marked with ●; clicking the other model stops the first server and starts the second (tooltip and About update accordingly).
- [ ] **Service toggle**: click "Service: On" to turn it off. Expected: icon turns black-and-white, tooltip shows "Stopped", Switch submenu becomes disabled/greyed. Click "Service: Off" to turn it back on. Expected: the **Default** model loads (not necessarily the last one you switched to), per SPEC.md §3.
- [ ] **Crash detection**: with Service on, kill the `llama-server.exe` process via Task Manager. Expected: within a few seconds a balloon notification appears, tray icon flips to black-and-white, Switch becomes disabled.
- [ ] **Start-with-Windows**: toggle the checkbox on in Settings, Save, then run `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v LlamaCppLauncher` in a terminal. Expected: the value exists and points at the launcher exe path in quotes. Toggle off, Save, re-run the `reg query` — expected: `ERROR: The system was unable to find the specified registry value.`
- [ ] **Single instance**: launch a second copy of the exe while the first is running. Expected: no second tray icon appears (or it silently exits) — only one instance is ever active.
- [ ] **Corrupted config**: with Service off, edit `%LOCALAPPDATA%\LlamaCppLauncher\config.json` to contain invalid JSON (e.g. delete a closing brace), relaunch. Expected: Settings opens automatically with an error banner instead of the app crashing.
- [ ] **Exit**: with Service on, right-click → Exit. Expected: `llama-server.exe` process is terminated (check Task Manager) and the tray icon disappears.

If any step fails, file it as a follow-up bug — do not consider the feature done until every item above passes on a real machine.
