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
