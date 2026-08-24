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
