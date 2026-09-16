// src/LlamaCppLauncher/Tray/TrayController.cs
using System.IO;
using LlamaCppLauncher.About;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Server;
using LlamaCppLauncher.Startup;
using LlamaCppLauncher.Validation;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using WpfControls = System.Windows.Controls;

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
    private readonly TrayContextMenuHost _menuHost = new();

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

        _notifyIcon.MouseClick += async (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                await ToggleServiceAsync();
            }
            else if (e.Button == Forms.MouseButtons.Right)
            {
                _menuHost.ShowMenu(BuildContextMenu(), Forms.Cursor.Position);
            }
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
                    // Prefer the explicit Default Model; if none is set, fall back to whatever was
                    // last running so a manual Start Service click isn't blocked just because the
                    // user never flipped the Default toggle in Settings.
                    ModelProfile? modelToStart = _config.Models.FirstOrDefault(m => m.IsDefault)
                        ?? _config.Models.FirstOrDefault(m => string.Equals(m.FileName, _config.LastRunningModelFileName, StringComparison.OrdinalIgnoreCase));

                    if (modelToStart is null)
                    {
                        ShowBalloon("llama.cpp Launcher", "No default model is configured. Open Settings to choose one.", isError: true);
                    }
                    else
                    {
                        await StartServerAsync(modelToStart);
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

    public async Task<bool> SwitchToAsync(string fileName)
    {
        if (_isTransitioning)
        {
            return false;
        }

        ModelProfile? target = _config.Models.FirstOrDefault(m =>
            string.Equals(m.FileName, fileName, StringComparison.OrdinalIgnoreCase));
        if (target is null)
        {
            return false;
        }

        _isTransitioning = true;
        bool started;
        try
        {
            StopServer();
            started = await StartServerAsync(target);
        }
        finally
        {
            _isTransitioning = false;
        }

        RefreshMenu();
        return started;
    }

    public void Stop()
    {
        StopServer();
        RefreshMenu();
    }

    public void ReloadConfigAfterSettingsSaved()
    {
        _config = _configService.Load() ?? _config;
        RefreshMenu();
    }

    // llama-server's TCP port can accept connections before its HTTP API is actually ready to
    // answer /v1/models with real data (the model may still be loading) — poll for a bit instead
    // of giving up on the first miss, especially right after a fresh start.
    private static readonly TimeSpan ApiReadyTimeout = TimeSpan.FromSeconds(30);

    public async Task<AboutViewModel> BuildAboutViewModelAsync()
    {
        if (!_processManager.IsRunning || _processManager.RunningModel is null)
        {
            return AboutViewModel.NotRunning();
        }

        string runningFileName = _processManager.RunningModel.FileName;
        DateTime deadline = DateTime.UtcNow + ApiReadyTimeout;
        while (true)
        {
            RunningModelInfo? info = await _apiClient.GetRunningModelInfoAsync(_config.Host, _config.Port).ConfigureAwait(false);
            if (info is not null)
            {
                return AboutViewModel.FromRunningModel(runningFileName, _config.Host, _config.Port, info);
            }

            if (!_processManager.IsRunning || DateTime.UtcNow >= deadline)
            {
                return AboutViewModel.NotRunning();
            }

            await Task.Delay(500).ConfigureAwait(false);
        }
    }

    private async Task<bool> StartServerAsync(ModelProfile profile)
    {
        try
        {
            _processManager.Start(_config, profile);
        }
        catch (Exception ex)
        {
            ShowBalloon("llama.cpp Launcher", $"Failed to start the server: {ex.Message}", isError: true);
            return false;
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
            return false;
        }

        _config.LastRunningModelFileName = profile.FileName;
        return true;
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

        // The context menu itself is built fresh in BuildContextMenu() at right-click time (see the
        // constructor's MouseClick handler) rather than cached here, so it's always in sync with
        // whatever _config/_processManager state is current at the moment the user actually opens it.
    }

    private WpfControls.ContextMenu BuildContextMenu()
    {
        bool isRunning = _processManager.IsRunning;
        ServiceState state = isRunning ? ServiceState.On : ServiceState.Off;
        TrayMenuState menuState = TrayMenuStateBuilder.Build(state, _config.Models, _config.LastRunningModelFileName);

        System.Windows.ResourceDictionary resources = System.Windows.Application.Current.Resources;
        var menu = new WpfControls.ContextMenu { Style = (System.Windows.Style)resources["TrayMenu.ContextMenuStyle"] };
        var itemStyle = (System.Windows.Style)resources["TrayMenu.MenuItemStyle"];

        var serviceItem = new WpfControls.MenuItem { Header = isRunning ? "Stop Service" : "Start Service", Style = itemStyle };
        serviceItem.Click += async (_, _) => await ToggleServiceAsync();
        menu.Items.Add(serviceItem);

        var statusItem = new WpfControls.MenuItem { Header = "Status", Style = itemStyle, FontWeight = System.Windows.FontWeights.Bold };
        statusItem.Click += (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(statusItem);

        var switchItem = new WpfControls.MenuItem { Header = "Switch", Style = itemStyle, IsEnabled = menuState.SwitchEnabled };
        foreach (SwitchMenuEntry entry in menuState.SwitchEntries)
        {
            var entryItem = new WpfControls.MenuItem
            {
                Header = entry.IsCurrent ? $"● {entry.Alias}" : entry.Alias,
                Style = itemStyle,
                FontWeight = entry.IsCurrent ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal
            };
            entryItem.Click += async (_, _) => await SwitchToAsync(entry.FileName);
            switchItem.Items.Add(entryItem);
        }
        menu.Items.Add(switchItem);

        var settingsItem = new WpfControls.MenuItem { Header = "Settings", Style = itemStyle };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(settingsItem);

        menu.Items.Add(new WpfControls.Separator { Style = (System.Windows.Style)resources["TrayMenu.SeparatorStyle"] });

        var exitItem = new WpfControls.MenuItem { Header = "Exit", Style = itemStyle };
        exitItem.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(exitItem);

        return menu;
    }

    private static string Truncate(string text) => text.Length <= 63 ? text : text[..63];

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _colorIcon.Dispose();
        _bwIcon.Dispose();
        _menuHost.Dispose();
    }
}
