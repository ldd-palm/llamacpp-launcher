using System.Text.Json.Serialization;

namespace LlamaCppLauncher.Server;

public sealed class ModelsApiResponse
{
    [JsonPropertyName("data")]
    public List<ModelsApiEntry> Data { get; set; } = new();
}

public sealed class ModelsApiEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("meta")]
    public ModelsApiMeta? Meta { get; set; }
}

public sealed class ModelsApiMeta
{
    [JsonPropertyName("n_ctx")]
    public int NCtx { get; set; }

    [JsonPropertyName("ftype")]
    public string? Ftype { get; set; }

    [JsonPropertyName("n_params")]
    public long NParams { get; set; }
}

public sealed record RunningModelInfo(string Alias, int ContextSize, string Quantization, string TotalParams);
