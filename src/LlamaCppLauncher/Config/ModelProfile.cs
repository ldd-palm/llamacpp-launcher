using CommunityToolkit.Mvvm.ComponentModel;

namespace LlamaCppLauncher.Config;

public sealed partial class ModelProfile : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    private int _ctxSize = 8192;

    [ObservableProperty]
    private int _nGpuLayers;

    [ObservableProperty]
    private string _kvCacheType = "q8_0";

    [ObservableProperty]
    private int? _threads;

    [ObservableProperty]
    private int _batchSize = 2048;

    [ObservableProperty]
    private bool _flashAttention;

    [ObservableProperty]
    private bool _isDefault;

    /// The literal command line invoked to launch this model, as generated (and possibly hand-edited)
    /// on the Models settings page. Empty means "not yet generated" — Start falls back to assembling
    /// one from the structured fields above (see LlamaServerArgumentBuilder.Build).
    [ObservableProperty]
    private string _commandLine = string.Empty;

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
            CommandLine = string.Empty
        };
    }
}
