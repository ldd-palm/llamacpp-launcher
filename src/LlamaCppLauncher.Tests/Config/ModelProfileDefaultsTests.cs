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
        Assert.Equal(string.Empty, profile.CommandLine);
    }
}
