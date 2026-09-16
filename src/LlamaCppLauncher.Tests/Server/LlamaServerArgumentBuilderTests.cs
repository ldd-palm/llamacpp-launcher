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
    public void Build_IncludesFlashAttentionFlagWithOnValue_WhenEnabled()
    {
        // Modern llama-server requires an explicit value here (-fa [on|off|auto]) — a bare
        // --flash-attn is a parse error that kills the process before it binds the port.
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.FlashAttention = true;

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        int flagIndex = args.IndexOf("--flash-attn");
        Assert.NotEqual(-1, flagIndex);
        Assert.Equal("on", args[flagIndex + 1]);
    }

    [Fact]
    public void Build_OmitsFlashAttentionFlag_WhenDisabled()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.DoesNotContain("--flash-attn", args);
    }

    [Fact]
    public void Build_UsesSavedCommandLine_WhenSet()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");
        profile.CommandLine = "-m \"C:\\custom\\model.gguf\" --port 9090 --custom-flag";

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Equal(new[] { "-m", @"C:\custom\model.gguf", "--port", "9090", "--custom-flag" }, args);
    }

    [Fact]
    public void Build_FallsBackToStructuredFields_WhenCommandLineEmpty()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        List<string> args = LlamaServerArgumentBuilder.Build(Config(), profile);

        Assert.Contains("-m", args);
        Assert.Contains(@"C:\models\model.gguf", args);
    }

    [Fact]
    public void GenerateCommandLine_RendersStructuredFieldsAsAQuotedString()
    {
        ModelProfile profile = ModelProfile.CreateDefault("model.gguf");

        string commandLine = LlamaServerArgumentBuilder.GenerateCommandLine(Config(), profile);

        Assert.Equal(
            "-m C:\\models\\model.gguf --host 127.0.0.1 --port 8080 -c 8192 -ngl 0 -ctk q8_0 -ctv q8_0 --alias model -b 2048",
            commandLine);
    }

    [Fact]
    public void GenerateCommandLine_RoundTripsThroughTokenize()
    {
        AppConfig config = Config();
        config.ModelsDirectory = @"C:\My Models";
        ModelProfile profile = ModelProfile.CreateDefault("model one.gguf");

        string commandLine = LlamaServerArgumentBuilder.GenerateCommandLine(config, profile);
        List<string> tokens = LlamaServerArgumentBuilder.Tokenize(commandLine);

        int modelIndex = tokens.IndexOf("-m") + 1;
        Assert.Equal(@"C:\My Models\model one.gguf", tokens[modelIndex]);
    }

    [Fact]
    public void Tokenize_SplitsOnWhitespace_AndStripsQuotesFromQuotedRuns()
    {
        List<string> tokens = LlamaServerArgumentBuilder.Tokenize("-m \"C:\\path with spaces\\model.gguf\"  --port   8080");

        Assert.Equal(new[] { "-m", @"C:\path with spaces\model.gguf", "--port", "8080" }, tokens);
    }
}
