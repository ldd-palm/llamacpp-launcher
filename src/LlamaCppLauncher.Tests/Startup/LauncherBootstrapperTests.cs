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
