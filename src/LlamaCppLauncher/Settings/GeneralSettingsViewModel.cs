using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;
using LlamaCppLauncher.Validation;
using PortStatus = LlamaCppLauncher.Validation.PortStatus;

namespace LlamaCppLauncher.Settings;

public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly IPortChecker _portChecker;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _modelsDirectory = string.Empty;

    [ObservableProperty]
    private int _port = 8080;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _theme = "Light";

    [ObservableProperty]
    private string? _executablePathStatus;

    [ObservableProperty]
    private bool _executablePathHasError;

    [ObservableProperty]
    private string? _modelsDirectoryStatus;

    [ObservableProperty]
    private bool _modelsDirectoryHasError;

    [ObservableProperty]
    private string? _portStatusText;

    [ObservableProperty]
    private bool _portHasError;

    /// Raised whenever Theme changes (including from FromConfig's initial assignment), so the
    /// composition root can apply it live without this ViewModel touching any WPF-UI theming API.
    public event EventHandler<string>? ThemeChanged;

    public GeneralSettingsViewModel(IPortChecker portChecker)
    {
        _portChecker = portChecker;
    }

    public static GeneralSettingsViewModel FromConfig(AppConfig config, IPortChecker portChecker)
    {
        var viewModel = new GeneralSettingsViewModel(portChecker)
        {
            ExecutablePath = config.ExecutablePath,
            ModelsDirectory = config.ModelsDirectory,
            Port = config.Port,
            StartWithWindows = config.StartWithWindows,
            Theme = string.IsNullOrWhiteSpace(config.Theme) ? "Light" : config.Theme
        };
        viewModel.RevalidateAll();
        return viewModel;
    }

    partial void OnExecutablePathChanged(string value) => RevalidateExecutablePath();
    partial void OnModelsDirectoryChanged(string value) => RevalidateModelsDirectory();
    partial void OnPortChanged(int value) => RevalidatePort();
    partial void OnThemeChanged(string value) => ThemeChanged?.Invoke(this, value);

    public void RevalidateAll()
    {
        RevalidateExecutablePath();
        RevalidateModelsDirectory();
        RevalidatePort();
    }

    private void RevalidateExecutablePath()
    {
        bool found = File.Exists(ExecutablePath);
        ExecutablePathHasError = !found;
        ExecutablePathStatus = found ? "llama-server.exe found." : "llama-server.exe not found at this location.";
    }

    private void RevalidateModelsDirectory()
    {
        if (!Directory.Exists(ModelsDirectory))
        {
            ModelsDirectoryStatus = "Directory not found.";
            ModelsDirectoryHasError = true;
            return;
        }

        int count = ModelDiscoveryService.DiscoverModelFiles(ModelsDirectory).Count;
        ModelsDirectoryHasError = count == 0;
        ModelsDirectoryStatus = count == 0
            ? "No .gguf files found."
            : $"Found {count} model{(count == 1 ? "" : "s")}.";
    }

    private void RevalidatePort()
    {
        PortStatus status = _portChecker.GetStatus(Port);
        switch (status)
        {
            case PortStatus.Free:
                PortStatusText = "Port is available.";
                PortHasError = false;
                break;
            case PortStatus.OccupiedByLauncher:
                PortStatusText = "In use by this launcher's server.";
                PortHasError = false;
                break;
            default:
                PortStatusText = "Port is already in use by another application.";
                PortHasError = true;
                break;
        }
    }

    public void ApplyTo(AppConfig config)
    {
        config.ExecutablePath = ExecutablePath;
        config.ModelsDirectory = ModelsDirectory;
        config.Port = Port;
        config.StartWithWindows = StartWithWindows;
        config.Theme = Theme;
    }
}
