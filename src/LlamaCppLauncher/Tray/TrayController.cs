// src/LlamaCppLauncher/Tray/TrayController.cs
using System.IO;
using LlamaCppLauncher.About;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Validation;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LlamaCppLauncher.Tray;

public sealed class TrayController : IDisposable
{
    private static readonly TimeSpan PortBindTimeout = TimeSpan.FromSeconds(10);

    private readonly ConfigService _configService;
    private readonly ValidationService _validationService;
    private readonly ITcpPortProbe _portProbe;
    private readonly LlamaServerProcessManager _processManager;
    private readonly LlamaServerApiClient _apiClient;
    private readonly Drawing.Icon _colorIcon;
    private readonly Drawing.Icon _bwIcon;
    private readonly Forms.NotifyIcon _notifyIcon;

    private AppConfig _config = new();
    private bool _isTransitioning;

    public event EventHandler? SettingsRequested;
    public event EventHandler? AboutRequested;
    public event EventHandler? ExitRequested;

    public AppConfig CurrentConfig => _config;

    public string? RunningModelFileName => _processManager.IsRunning ? _processManager.RunningModel?.FileName : null;

    public TrayController(
        ConfigService configService,
        ValidationService validationService,
        ITcpPortProbe portProbe,
        LlamaServerProcessManager processManager,
        LlamaServerApiClient apiClient)
    {
        _configService = configService;
        _validationService = validationService;
        _portProbe = portProbe;
        _processManager = processManager;
        _apiClient = apiClient;

        _colorIcon = LoadIcon("cpp_logo_color.ico");
        _bwIcon = LoadIcon("cpp_logo_bw.ico");

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _bwIcon,
            Text = "llama.cpp Launcher — Stopped",
            Visible = true
        };

        _processManager.ServerExited += (_, _) =>
        {
            ShowBalloon("llama.cpp Launcher", "The server process exited unexpectedly. Check the error log for details.", isError: true);
            RefreshMenu();
        };
    }

    private static Drawing.Icon LoadIcon(string fileName)
    {
        using Stream stream = typeof(TrayController).Assembly
            .GetManifestResourceStream($"LlamaCppLauncher.Assets.{fileName}")!;
        return new Drawing.Icon(stream);
    }

    public async Task ApplyStartupDecisionAsync(StartupDecision decision)
    {
        if (decision.Config is not null)
        {
            _config = decision.Config;
        }

        if (decision.Action == StartupAction.AutoStartDefaultModel && decision.DefaultModel is not null)
        {
            await StartServerAsync(decision.DefaultModel);
        }
        else if (decision.Message is not null && decision.Action == StartupAction.StayOffWithNotification)
        {
            ShowBalloon("llama.cpp Launcher", decision.Message, isError: true);
        }

        RefreshMenu();
    }

    public async Task ToggleServiceAsync()
    {
        if (_isTransitioning)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            if (_processManager.IsRunning)
            {
                StopServer();
            }
            else
            {
                ValidationResult validation = _validationService.ValidateGeneral(_config);
                if (!validation.IsValid)
                {
                    ShowBalloon("llama.cpp Launcher", string.Join(" ", validation.Errors), isError: true);
                }
                else
                {
                    ModelProfile? defaultModel = _config.Models.FirstOrDefault(m => m.IsDefault);
                    if (defaultModel is null)
                    {
                        ShowBalloon("llama.cpp Launcher", "No default model is configured. Open Settings to choose one.", isError: true);
                    }
                    else
                    {
                        await StartServerAsync(defaultModel);
                    }
                }
            }
        }
        finally
        {
            _isTransitioning = false;
        }

        RefreshMenu();
    }

    public async Task SwitchToAsync(string fileName)
    {
        if (_isTransitioning)
        {
            return;
        }

        ModelProfile? target = _config.Models.FirstOrDefault(m =>
            string.Equals(m.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return;
        }

        _isTransitioning = true;
        try
        {
            StopServer();
            await StartServerAsync(target);
        }
        finally
        {
            _isTransitioning = false;
        }

        RefreshMenu();
    }

    public void ReloadConfigAfterSettingsSaved()
    {
        _config = _configService.Load() ?? _config;
        RefreshMenu();
    }

    public AboutViewModel BuildAboutViewModel()
    {
        if (!_processManager.IsRunning || _processManager.RunningModel is null)
        {
            return AboutViewModel.NotRunning();
        }

        RunningModelInfo? info = _apiClient
            .GetRunningModelInfoAsync(_config.Host, _config.Port)
            .GetAwaiter()
            .GetResult();

        return info is null
            ? AboutViewModel.NotRunning()
            : AboutViewModel.FromRunningModel(_processManager.RunningModel.FileName, _config.Host, _config.Port, info);
    }

    private async Task StartServerAsync(ModelProfile profile)
    {
        try
        {
            _processManager.Start(_config, profile);
        }
        catch (Exception ex)
        {
            ShowBalloon("llama.cpp Launcher", $"Failed to start the server: {ex.Message}", isError: true);
            return;
        }

        if (!await WaitForPortToBindAsync())
        {
            if (_processManager.IsRunning)
            {
                StopServer();
                ShowBalloon(
                    "llama.cpp Launcher",
                    $"The server did not start listening on port {_config.Port} within {PortBindTimeout.TotalSeconds:0} seconds. Check the error log for details.",
                    isError: true);
            }

            // If the process already exited, the ServerExited handler already showed
            // an accurate balloon and refreshed the menu — avoid a duplicate notification.
            return;
        }

        _config.LastRunningModelFileName = profile.FileName;
    }

    private async Task<bool> WaitForPortToBindAsync()
    {
        DateTime deadline = DateTime.UtcNow + PortBindTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_portProbe.IsPortListening(_config.Port))
            {
                return true;
            }
            if (!_processManager.IsRunning)
            {
                return false;
            }
            await Task.Delay(250);
        }
        return _portProbe.IsPortListening(_config.Port);
    }

    private void StopServer() => _processManager.Stop();

    private void ShowBalloon(string title, string message, bool isError)
    {
        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(5000, title, message, isError ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info);
    }

    private void RefreshMenu()
    {
        bool isRunning = _processManager.IsRunning;

        _notifyIcon.Icon = isRunning ? _colorIcon : _bwIcon;
        _notifyIcon.Text = Truncate(isRunning && _processManager.RunningModel is not null
            ? $"llama.cpp Launcher — Running ({_processManager.RunningModel.Alias})"
            : "llama.cpp Launcher — Stopped");

        ServiceState state = isRunning ? ServiceState.On : ServiceState.Off;
        TrayMenuState menuState = TrayMenuStateBuilder.Build(state, _config.Models, _config.LastRunningModelFileName);

        var menu = new Forms.ContextMenuStrip();

        var serviceItem = new Forms.ToolStripMenuItem(isRunning ? "Service: On" : "Service: Off");
        serviceItem.Click += async (_, _) => await ToggleServiceAsync();
        menu.Items.Add(serviceItem);

        var settingsItem = new Forms.ToolStripMenuItem("Settings");
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        var switchItem = new Forms.ToolStripMenuItem("Switch") { Enabled = menuState.SwitchEnabled };
        foreach (SwitchMenuEntry entry in menuState.SwitchEntries)
        {
            var entryItem = new Forms.ToolStripMenuItem(entry.IsCurrent ? $"● {entry.Alias}" : entry.Alias)
            {
                Font = entry.IsCurrent
                    ? new Drawing.Font(Forms.Control.DefaultFont, Drawing.FontStyle.Bold)
                    : Forms.Control.DefaultFont
            };
            entryItem.Click += async (_, _) => await SwitchToAsync(entry.FileName);
            switchItem.DropDownItems.Add(entryItem);
        }
        menu.Items.Add(switchItem);

        var aboutItem = new Forms.ToolStripMenuItem("About");
        aboutItem.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(aboutItem);

        menu.Items.Add(new Forms.ToolStripSeparator());

        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        Forms.ContextMenuStrip? previousMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.ContextMenuStrip = menu;
        previousMenu?.Dispose();
    }

    private static string Truncate(string text) => text.Length <= 63 ? text : text[..63];

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _colorIcon.Dispose();
        _bwIcon.Dispose();
    }
}
