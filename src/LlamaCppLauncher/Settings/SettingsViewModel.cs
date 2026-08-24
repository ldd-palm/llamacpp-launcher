using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;

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
    private void BrowseExecutable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "llama-server executable (*.exe)|*.exe",
            FileName = General.ExecutablePath
        };
        if (dialog.ShowDialog() == true)
        {
            General.ExecutablePath = dialog.FileName;
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
