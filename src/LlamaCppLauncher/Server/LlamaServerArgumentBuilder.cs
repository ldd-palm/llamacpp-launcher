using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Server;

public static class LlamaServerArgumentBuilder
{
    public static List<string> Build(AppConfig config, ModelProfile profile)
    {
        string modelPath = Path.Combine(config.ModelsDirectory, profile.FileName);

        var args = new List<string>
        {
            "-m", modelPath,
            "--host", config.Host,
            "--port", config.Port.ToString(),
            "-c", profile.CtxSize.ToString(),
            "-ngl", profile.NGpuLayers.ToString(),
            "-ctk", profile.KvCacheType,
            "-ctv", profile.KvCacheType,
            "--alias", profile.Alias,
            "-b", profile.BatchSize.ToString()
        };

        if (profile.Threads.HasValue)
        {
            args.Add("--threads");
            args.Add(profile.Threads.Value.ToString());
        }

        if (profile.FlashAttention)
        {
            args.Add("--flash-attn");
        }

        if (!string.IsNullOrWhiteSpace(profile.ExtraArguments))
        {
            args.AddRange(profile.ExtraArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        return args;
    }
}
