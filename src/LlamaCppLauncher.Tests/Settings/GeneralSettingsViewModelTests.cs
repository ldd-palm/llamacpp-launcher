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

        Assert.True(viewModel.ExecutablePathHasError);
        Assert.Equal("llama-server.exe not found at this location.", viewModel.ExecutablePathStatus);
    }

    [Fact]
    public void RevalidateExecutablePath_ClearsError_WhenFileExists()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ExecutablePath = _exePath;

        Assert.False(viewModel.ExecutablePathHasError);
        Assert.Equal("llama-server.exe found.", viewModel.ExecutablePathStatus);
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
        Assert.False(viewModel.ExecutablePathHasError);
        Assert.Equal("Found 1 model.", viewModel.ModelsDirectoryStatus);
        Assert.True(viewModel.StartWithWindows);
        // Port (8080) equals the ViewModel field's own compile-time default, so CommunityToolkit.Mvvm's
        // generated setter would skip OnPortChanged/RevalidatePort for this field specifically if FromConfig
        // relied only on the object-initializer assignment. PortStatusText defaults to null and is only ever
        // set by RevalidatePort, so asserting it here is what actually proves RevalidateAll() ran.
        Assert.Equal("Port is available.", viewModel.PortStatusText);
    }

    [Fact]
    public void RevalidateModelsDirectory_ReportsDirectoryNotFound_WhenDirectoryMissing()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ModelsDirectory = Path.Combine(_tempDir, "missing-dir");

        Assert.Equal("Directory not found.", viewModel.ModelsDirectoryStatus);
    }

    [Fact]
    public void RevalidateModelsDirectory_ReportsNoGgufFiles_WhenDirectoryIsEmpty()
    {
        string emptyDir = Path.Combine(_tempDir, "empty-models");
        Directory.CreateDirectory(emptyDir);
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        viewModel.ModelsDirectory = emptyDir;

        Assert.Equal("No .gguf files found.", viewModel.ModelsDirectoryStatus);
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

    [Fact]
    public void Theme_DefaultsToLight()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));

        Assert.Equal("Light", viewModel.Theme);
    }

    [Fact]
    public void FromConfig_DefaultsThemeToLight_WhenConfigThemeIsBlank()
    {
        var config = new AppConfig { ExecutablePath = _exePath, ModelsDirectory = _modelsDir, Theme = "" };

        var viewModel = GeneralSettingsViewModel.FromConfig(config, new StubPortChecker(PortStatus.Free));

        Assert.Equal("Light", viewModel.Theme);
    }

    [Fact]
    public void FromConfig_UsesConfiguredTheme_WhenSet()
    {
        var config = new AppConfig { ExecutablePath = _exePath, ModelsDirectory = _modelsDir, Theme = "Dark" };

        var viewModel = GeneralSettingsViewModel.FromConfig(config, new StubPortChecker(PortStatus.Free));

        Assert.Equal("Dark", viewModel.Theme);
    }

    [Fact]
    public void ApplyTo_CopiesThemeIntoConfig()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free)) { Theme = "Dark" };
        var config = new AppConfig();

        viewModel.ApplyTo(config);

        Assert.Equal("Dark", config.Theme);
    }

    [Fact]
    public void ThemeChanged_FiresWithNewValue_WhenThemeChanges()
    {
        var viewModel = new GeneralSettingsViewModel(new StubPortChecker(PortStatus.Free));
        string? raised = null;
        viewModel.ThemeChanged += (_, theme) => raised = theme;

        viewModel.Theme = "Dark";

        Assert.Equal("Dark", raised);
    }
}
