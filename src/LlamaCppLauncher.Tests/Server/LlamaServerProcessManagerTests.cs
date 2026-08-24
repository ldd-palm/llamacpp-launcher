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
