using System.Diagnostics;

namespace LlamaCppLauncher.Validation;

public sealed class PortCheckService : IPortChecker
{
    private readonly ITcpPortProbe _portProbe;
    private readonly string _launcherExecutablePath;

    public PortCheckService(ITcpPortProbe portProbe, string launcherExecutablePath)
    {
        _portProbe = portProbe;
        _launcherExecutablePath = launcherExecutablePath;
    }

    public PortStatus GetStatus(int port)
    {
        bool isListening = _portProbe.IsPortListening(port);
        bool isOwnedByLauncher = isListening && IsLauncherManagedProcessRunning();
        return PortStatusClassifier.Classify(isListening, isOwnedByLauncher);
    }

    private bool IsLauncherManagedProcessRunning()
    {
        if (string.IsNullOrEmpty(_launcherExecutablePath))
        {
            return false;
        }

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, _launcherExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Access denied reading MainModule for some processes (elevation/other user) — skip them.
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }
}
