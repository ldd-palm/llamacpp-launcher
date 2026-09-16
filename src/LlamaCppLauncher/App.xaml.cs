// src/LlamaCppLauncher/App.xaml.cs
using System.IO;
using System.Net.Http;
using System.Windows;
using LlamaCppLauncher.About;
using LlamaCppLauncher.AutoStart;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;
using LlamaCppLauncher.Server;
using LlamaCppLauncher.Settings;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Tray;
using LlamaCppLauncher.Validation;
using Wpf.Ui.Appearance;

namespace LlamaCppLauncher;

public partial class App : System.Windows.Application
{
    // Portable layout: config.json, logs\, and the exe itself all live together in whatever folder
    // the app was launched from, instead of %LOCALAPPDATA%.
    private static readonly string AppDirectory = AppContext.BaseDirectory;

    private SingleInstanceService? _singleInstance;
    private TrayController? _trayController;
    private HttpClient? _httpClient;
    private ConfigService? _configService;
    private AutoStartService? _autoStartService;
    private LlamaServerProcessManager? _processManager;
    private SettingsWindow? _settingsWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstanceService("LlamaCppLauncher_SingleInstance_Mutex");
        if (!_singleInstance.IsFirstInstance)
        {
            Shutdown();
            return;
        }

        string configPath = Path.Combine(AppDirectory, "config.json");
        string logDirectory = Path.Combine(AppDirectory, "logs");
        Directory.CreateDirectory(logDirectory);

        _configService = new ConfigService(configPath);
        var portProbe = new TcpPortProbe();
        var portChecker = new PortCheckService(portProbe, () => _trayController?.CurrentConfig.ExecutablePath ?? GetConfiguredExecutablePath(_configService));
        var validationService = new ValidationService(portChecker);
        _processManager = new LlamaServerProcessManager(
            Path.Combine(logDirectory, "llama-server.out.log"),
            Path.Combine(logDirectory, "llama-server.err.log"));
        _autoStartService = new AutoStartService(new RegistryRunKeyStore());
        _httpClient = new HttpClient();
        var apiClient = new LlamaServerApiClient(_httpClient);

        _trayController = new TrayController(_configService, validationService, portProbe, _processManager, apiClient);
        _trayController.SettingsRequested += (_, _) => OpenSettings();
        _trayController.AboutRequested += async (_, _) => await OpenAboutAsync();
        _trayController.ExitRequested += (_, _) => ExitApplication();

        var bootstrapper = new LauncherBootstrapper(_configService, validationService);
        StartupDecision decision = bootstrapper.Decide();
        await _trayController.ApplyStartupDecisionAsync(decision);

        if (decision.Action == StartupAction.OpenSettingsWithError)
        {
            OpenSettings();
        }
    }

    private static string GetConfiguredExecutablePath(ConfigService configService)
    {
        try
        {
            return configService.Load()?.ExecutablePath ?? string.Empty;
        }
        catch (ConfigLoadException)
        {
            return string.Empty;
        }
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        AppConfig config = _trayController!.CurrentConfig;
        ApplyTheme(config.Theme);

        IReadOnlyList<string> discovered = ModelDiscoveryService.DiscoverModelFiles(config.ModelsDirectory);
        List<ModelProfile> mergedModels = ModelDiscoveryService.MergeWithConfiguredModels(discovered, config.Models);

        var portChecker = new PortCheckService(new TcpPortProbe(), () => config.ExecutablePath);
        GeneralSettingsViewModel generalViewModel = GeneralSettingsViewModel.FromConfig(config, portChecker);
        var modelsViewModel = new ModelsSettingsViewModel(mergedModels);
        var settingsViewModel = new SettingsViewModel(
            config, generalViewModel, modelsViewModel, _configService!, _autoStartService!, _trayController!.RunningModelFileName);

        settingsViewModel.StartModelRequested += async (_, fileName) => await StartModelFromSettingsAsync(fileName);
        settingsViewModel.StopRequested += async (_, _) => await StopModelFromSettingsAsync();
        settingsViewModel.StatusRequested += async (_, _) => await OpenAboutAsync();
        generalViewModel.ThemeChanged += (_, theme) => ApplyTheme(theme);

        _settingsWindow = new SettingsWindow(settingsViewModel);
        _settingsWindow.Closed += (_, _) =>
        {
            _trayController!.ReloadConfigAfterSettingsSaved();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
    }

    private async Task StartModelFromSettingsAsync(string fileName)
    {
        bool started = await _trayController!.SwitchToAsync(fileName);
        if (started)
        {
            await OpenAboutAsync();
        }
    }

    private async Task OpenAboutAsync()
    {
        ApplyTheme(_trayController!.CurrentConfig.Theme);
        var window = new AboutWindow(await _trayController.BuildAboutViewModelAsync());
        window.Show();
    }

    private static void ApplyTheme(string? theme)
    {
        ApplicationTheme appTheme = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
        ApplicationThemeManager.Apply(appTheme);
    }

    private async Task StopModelFromSettingsAsync()
    {
        _trayController!.Stop();
        await OpenAboutAsync();
    }

    private void ExitApplication()
    {
        // Always attempt to unload the running model before anything else, and keep going through
        // the rest of teardown even if that attempt throws — a failed kill shouldn't leave the
        // launcher itself hung or orphan the mutex/tray icon.
        try
        {
            _processManager?.Stop();
        }
        catch (Exception)
        {
        }

        _trayController?.Dispose();
        _singleInstance?.Dispose();
        _httpClient?.Dispose();
        Shutdown();
    }
}
