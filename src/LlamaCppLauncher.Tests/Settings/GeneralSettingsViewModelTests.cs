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
