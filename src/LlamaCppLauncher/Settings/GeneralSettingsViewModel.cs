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
    private string? _executablePathError;

    [ObservableProperty]
    private string? _modelsDirectoryStatus;

    [ObservableProperty]
    private string? _portStatusText;

    [ObservableProperty]
    private bool _portHasError;

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
            StartWithWindows = config.StartWithWindows
        };
        viewModel.RevalidateAll();
        return viewModel;
    }

    partial void OnExecutablePathChanged(string value) => RevalidateExecutablePath();
    partial void OnModelsDirectoryChanged(string value) => RevalidateModelsDirectory();
    partial void OnPortChanged(int value) => RevalidatePort();

    public void RevalidateAll()
    {
        RevalidateExecutablePath();
        RevalidateModelsDirectory();
        RevalidatePort();
    }

    private void RevalidateExecutablePath()
    {
        ExecutablePathError = File.Exists(ExecutablePath) ? null : "File not found.";
    }

    private void RevalidateModelsDirectory()
    {
        if (!Directory.Exists(ModelsDirectory))
        {
            ModelsDirectoryStatus = "Directory not found.";
            return;
        }

        int count = ModelDiscoveryService.DiscoverModelFiles(ModelsDirectory).Count;
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
    }
}
