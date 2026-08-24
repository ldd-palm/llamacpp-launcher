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
