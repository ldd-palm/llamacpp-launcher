using System;

namespace LlamaCppLauncher.Config;

public sealed class ModelProfile
{
    public string FileName { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public int CtxSize { get; set; } = 8192;
    public int NGpuLayers { get; set; }
    public string KvCacheType { get; set; } = "q8_0";
    public int? Threads { get; set; }
    public int BatchSize { get; set; } = 2048;
    public bool FlashAttention { get; set; }
    public bool IsDefault { get; set; }
    public string ExtraArguments { get; set; } = string.Empty;

    public static ModelProfile CreateDefault(string fileName)
    {
        return new ModelProfile
        {
            FileName = fileName,
            Alias = System.IO.Path.GetFileNameWithoutExtension(fileName),
            CtxSize = 8192,
            NGpuLayers = 0,
            KvCacheType = "q8_0",
            Threads = null,
            BatchSize = 2048,
            FlashAttention = false,
            IsDefault = false,
            ExtraArguments = string.Empty
        };
    }
}
