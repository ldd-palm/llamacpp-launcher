using System.Diagnostics;
using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Server;

public sealed class LlamaServerProcessManager : IDisposable
{
    private readonly string _outLogPath;
    private readonly string _errLogPath;
    private Process? _process;

    public event EventHandler? ServerExited;

    public bool IsRunning => _process is { HasExited: false };
    public ModelProfile? RunningModel { get; private set; }

    public LlamaServerProcessManager(string outLogPath, string errLogPath)
    {
        _outLogPath = outLogPath;
        _errLogPath = errLogPath;
    }

    public void Start(AppConfig config, ModelProfile profile)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("A server process is already running. Stop it before starting another.");
        }

        string? logDirectory = Path.GetDirectoryName(_outLogPath);
        if (!string.IsNullOrEmpty(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = config.ExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(config.ExecutablePath),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string arg in LlamaServerArgumentBuilder.Build(config, profile))
        {
            startInfo.ArgumentList.Add(arg);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += OnProcessExited;

        process.Start();

        var outWriter = new StreamWriter(_outLogPath, append: false) { AutoFlush = true };
        var errWriter = new StreamWriter(_errLogPath, append: false) { AutoFlush = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) outWriter.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) errWriter.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _process = process;
        RunningModel = profile;
    }

    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process.Exited -= OnProcessExited;
        _process.Dispose();
        _process = null;
        RunningModel = null;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        RunningModel = null;
        ServerExited?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
