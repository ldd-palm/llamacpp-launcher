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
