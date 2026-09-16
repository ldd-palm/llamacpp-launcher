using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _configService;
    private readonly AutoStartService _autoStartService;
    private readonly AppConfig _config;
    private readonly string? _runningModelFileName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModelsPageSelected))]
    private bool _isGeneralPageSelected = true;

    public bool IsModelsPageSelected => !IsGeneralPageSelected;

    [ObservableProperty]
    private string? _saveNotice;

    public GeneralSettingsViewModel General { get; }
    public ModelsSettingsViewModel Models { get; }

    /// Raised when the user clicks Start on the Models page, carrying the selected model's file name.
    public event EventHandler<string>? StartModelRequested;

    /// Raised when the user clicks Stop on the Models page.
    public event EventHandler? StopRequested;

    /// Raised when the user clicks Status on the General page.
    public event EventHandler? StatusRequested;

    public SettingsViewModel(
        AppConfig config,
        GeneralSettingsViewModel general,
        ModelsSettingsViewModel models,
        ConfigService configService,
        AutoStartService autoStartService,
        string? runningModelFileName = null)
    {
        _config = config;
        General = general;
        Models = models;
        _configService = configService;
        _autoStartService = autoStartService;
        _runningModelFileName = runningModelFileName;

        General.PropertyChanged += OnGeneralPropertyChanged;
    }

    private void OnGeneralPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GeneralSettingsViewModel.ModelsDirectory))
        {
            Models.RefreshFromDirectory(General.ModelsDirectory);
        }
    }

    [RelayCommand]
    private void ShowGeneralPage() => IsGeneralPageSelected = true;

    [RelayCommand]
    private void ShowModelsPage() => IsGeneralPageSelected = false;

    [RelayCommand]
    private void Save()
    {
        General.ApplyTo(_config);
        Models.ApplyTo(_config);
        _configService.Save(_config);
        _autoStartService.SetEnabled(_config.StartWithWindows, Environment.ProcessPath ?? string.Empty);

        // SPEC.md §4.3: saving params for the currently-running model does not auto-restart the
        // server — surface that explicitly instead of silently doing nothing.
        SaveNotice = _runningModelFileName is not null && Models.Models.Any(m => m.FileName == _runningModelFileName)
            ? "Changes will apply the next time this model is started."
            : null;
    }

    [RelayCommand]
    private void Start()
    {
        if (Models.SelectedModel is null)
        {
            return;
        }

        Save();
        StartModelRequested?.Invoke(this, Models.SelectedModel.FileName);
    }

    [RelayCommand]
    private void Stop()
    {
        StopRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Status()
    {
        StatusRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void GenerateCommandLine()
    {
        if (Models.SelectedModel is null)
        {
            return;
        }

        var snapshot = new AppConfig
        {
            Host = _config.Host,
            Port = General.Port,
            ModelsDirectory = General.ModelsDirectory
        };
        Models.SelectedModel.CommandLine = LlamaServerArgumentBuilder.GenerateCommandLine(snapshot, Models.SelectedModel);
    }

    [RelayCommand]
    private void BrowseExecutable()
    {
        string? currentDirectory = Path.GetDirectoryName(General.ExecutablePath);
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            FolderName = !string.IsNullOrEmpty(currentDirectory) && Directory.Exists(currentDirectory) ? currentDirectory : string.Empty
        };
        if (dialog.ShowDialog() == true)
        {
            General.ExecutablePath = Path.Combine(dialog.FolderName, "llama-server.exe");
        }
    }

    [RelayCommand]
    private void BrowseModelsDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { FolderName = General.ModelsDirectory };
        if (dialog.ShowDialog() == true)
        {
            General.ModelsDirectory = dialog.FolderName;
        }
    }
}
