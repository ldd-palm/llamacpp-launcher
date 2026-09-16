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

    [Fact]
    public void StartCommand_SavesConfig_AndRaisesStartModelRequested_WithSelectedModelFileName()
    {
        var models = new ModelsSettingsViewModel(new[] { ModelProfile.CreateDefault("a.gguf") });
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)) { ExecutablePath = "exe.exe", ModelsDirectory = "models" },
            models,
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        string? requestedFileName = null;
        viewModel.StartModelRequested += (_, fileName) => requestedFileName = fileName;

        viewModel.StartCommand.Execute(null);

        Assert.Equal("a.gguf", requestedFileName);
        Assert.NotNull(new ConfigService(_configPath).Load());
    }

    [Fact]
    public void StartCommand_DoesNothing_WhenNoModelIsSelected()
    {
        var models = new ModelsSettingsViewModel(Array.Empty<ModelProfile>());
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)),
            models,
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        bool raised = false;
        viewModel.StartModelRequested += (_, _) => raised = true;

        viewModel.StartCommand.Execute(null);

        Assert.False(raised);
        Assert.Null(new ConfigService(_configPath).Load());
    }

    [Fact]
    public void StopCommand_RaisesStopRequested()
    {
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)),
            new ModelsSettingsViewModel(Array.Empty<ModelProfile>()),
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        bool raised = false;
        viewModel.StopRequested += (_, _) => raised = true;

        viewModel.StopCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void StatusCommand_RaisesStatusRequested()
    {
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)),
            new ModelsSettingsViewModel(Array.Empty<ModelProfile>()),
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        bool raised = false;
        viewModel.StatusRequested += (_, _) => raised = true;

        viewModel.StatusCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void GenerateCommandLineCommand_AssemblesCommandLine_UsingLiveGeneralFields()
    {
        var models = new ModelsSettingsViewModel(new[] { ModelProfile.CreateDefault("model.gguf") });
        var general = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free))
        {
            ModelsDirectory = @"C:\models",
            Port = 9090
        };
        var viewModel = new SettingsViewModel(
            new AppConfig(), general, models, new ConfigService(_configPath), new AutoStartService(new InMemoryRunKeyStore()));

        viewModel.GenerateCommandLineCommand.Execute(null);

        Assert.Contains(@"C:\models\model.gguf", models.SelectedModel!.CommandLine);
        Assert.Contains("--port 9090", models.SelectedModel!.CommandLine);
    }

    [Fact]
    public void GenerateCommandLineCommand_DoesNothing_WhenNoModelIsSelected()
    {
        var models = new ModelsSettingsViewModel(Array.Empty<ModelProfile>());
        var viewModel = new SettingsViewModel(
            new AppConfig(),
            new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)),
            models,
            new ConfigService(_configPath),
            new AutoStartService(new InMemoryRunKeyStore()));

        viewModel.GenerateCommandLineCommand.Execute(null);

        Assert.Null(models.SelectedModel);
    }

    [Fact]
    public void ChangingGeneralModelsDirectory_RescansModelsList()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "LlamaCppLauncherTests_" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "found.gguf"), "");

            var general = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));
            var models = new ModelsSettingsViewModel(Array.Empty<ModelProfile>());
            var viewModel = new SettingsViewModel(
                new AppConfig(), general, models, new ConfigService(_configPath), new AutoStartService(new InMemoryRunKeyStore()));

            general.ModelsDirectory = tempDir;

            Assert.False(models.HasNoModels);
            Assert.Contains(models.Models, m => m.FileName == "found.gguf");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
