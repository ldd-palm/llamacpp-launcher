using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class LlamaServerArgumentBuilderTests
{
    private static AppConfig Config() => new()
    {
        ModelsDirectory = @"C:\models",
        Host = "127.0.0.1",
        Port = 8080
    };

    [Fact]
    public void Build_IncludesCoreArguments()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Equal(new[]
        {
            "-m", @"C:\models\model.gguf",
            "--host", "127.0.0.1",
            "--port", "8080",
            "-c", "8192",
            "-ngl", "0",
            "-ctk", "q8_0",
            "-ctv", "q8_0",
            "--alias", "model",
            "-b", "2048"
        }, args);
    }

    [Fact]
    public void Build_IncludesThreads_WhenSet()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.Threads = 8;

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Contains("--threads", args);
        Assert.Contains("8", args);
    }

    [Fact]
    public void Build_OmitsThreads_WhenNull()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.DoesNotContain("--threads", args);
    }

    [Fact]
    public void Build_IncludesFlashAttentionFlag_WhenEnabled()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.FlashAttention = true;

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Contains("--flash-attn", args);
    }

    [Fact]
    public void Build_OmitsFlashAttentionFlag_WhenDisabled()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.DoesNotContain("--flash-attn", args);
    }

    [Fact]
    public void Build_AppendsExtraArguments_SplitOnWhitespace()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.ExtraArguments = "--tensor-split 0.5,0.5";

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Equal("--tensor-split", args[^2]);
        Assert.Equal("0.5,0.5", args[^1]);
    }
}
