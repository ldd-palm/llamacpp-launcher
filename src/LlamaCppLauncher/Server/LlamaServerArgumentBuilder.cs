using System.IO;
using System.Text;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Server;

public static class LlamaServerArgumentBuilder
{
    /// What actually gets passed to Process.Start: the model's saved CommandLine if it has one
    /// (tokenized), otherwise a command assembled from the structured fields.
    public static List<string> Build(AppConfig config, ModelProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.CommandLine)
            ? BuildFromStructuredFields(config, profile)
            : Tokenize(profile.CommandLine);
    }

    /// Renders the structured fields into an editable command-line string — this is what the Models
    /// page's "Generate" button puts in the command-line box for the user to review/tweak before saving.
    public static string GenerateCommandLine(AppConfig config, ModelProfile profile)
    {
        return string.Join(' ', BuildFromStructuredFields(config, profile).Select(QuoteIfNeeded));
    }

    private static List<string> BuildFromStructuredFields(AppConfig config, ModelProfile profile)
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
            // Modern llama-server builds require an explicit value here (-fa [on|off|auto]) — a bare
            // --flash-attn is a parse error that kills the process before it ever binds the port.
            args.Add("--flash-attn");
            args.Add("on");
        }

        return args;
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Length == 0 || arg.Any(char.IsWhiteSpace) ? $"\"{arg}\"" : arg;

    /// Splits a command line on whitespace, treating a double-quoted run as a single token and
    /// stripping the quote characters — so a hand-edited CommandLine with a spaced path (or the
    /// output of GenerateCommandLine, which quotes for exactly this reason) turns back into a clean
    /// ProcessStartInfo.ArgumentList.
    public static List<string> Tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;

        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                    hasToken = false;
                }
                continue;
            }

            current.Append(c);
            hasToken = true;
        }

        if (hasToken)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
