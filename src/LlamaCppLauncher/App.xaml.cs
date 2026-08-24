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

namespace LlamaCppLauncher;

public partial class App : System.Windows.Application
{
    private static readonly string AppDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaCppLauncher");

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

        Directory.CreateDirectory(AppDataDirectory);
        string configPath = Path.Combine(AppDataDirectory, "config.json");
        string logDirectory = Path.Combine(AppDataDirectory, "logs");
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
        _trayController.AboutRequested += (_, _) => OpenAbout();
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
        IReadOnlyList<string> discovered = ModelDiscoveryService.DiscoverModelFiles(config.ModelsDirectory);
        List<ModelProfile> mergedModels = ModelDiscoveryService.MergeWithConfiguredModels(discovered, config.Models);

        var portChecker = new PortCheckService(new TcpPortProbe(), () => config.ExecutablePath);
        GeneralSettingsViewModel generalViewModel = GeneralSettingsViewModel.FromConfig(config, portChecker);
        var modelsViewModel = new ModelsSettingsViewModel(mergedModels);
        var settingsViewModel = new SettingsViewModel(
            config, generalViewModel, modelsViewModel, _configService!, _autoStartService!, _trayController!.RunningModelFileName);

        _settingsWindow = new SettingsWindow(settingsViewModel);
        _settingsWindow.Closed += (_, _) =>
        {
            _trayController!.ReloadConfigAfterSettingsSaved();
            _settingsWindow = null;
        };
        _settingsWindow.Show();
    }

    private void OpenAbout()
    {
        var window = new AboutWindow(_trayController!.BuildAboutViewModel());
        window.Show();
    }

    private void ExitApplication()
    {
        _processManager?.Stop();
        _trayController?.Dispose();
        _singleInstance?.Dispose();
        _httpClient?.Dispose();
        Shutdown();
    }
}
