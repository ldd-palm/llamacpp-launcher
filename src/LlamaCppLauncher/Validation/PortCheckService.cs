using System.Diagnostics;

namespace LlamaCppLauncher.Validation;

public sealed class PortCheckService : IPortChecker
{
    private readonly ITcpPortProbe _portProbe;
    private readonly Func<string> _launcherExecutablePathProvider;

    public PortCheckService(ITcpPortProbe portProbe, Func<string> launcherExecutablePathProvider)
    {
        _portProbe = portProbe;
        _launcherExecutablePathProvider = launcherExecutablePathProvider;
    }

    public PortStatus GetStatus(int port)
    {
        bool isListening = _portProbe.IsPortListening(port);
        bool isOwnedByLauncher = isListening && IsLauncherManagedProcessRunning();
        return PortStatusClassifier.Classify(isListening, isOwnedByLauncher);
    }

    private bool IsLauncherManagedProcessRunning()
    {
        string launcherExecutablePath = _launcherExecutablePathProvider();
        if (string.IsNullOrEmpty(launcherExecutablePath))
        {
            return false;
        }

        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, launcherExecutablePath, StringComparison.OrdinalIgnoreCase))
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
