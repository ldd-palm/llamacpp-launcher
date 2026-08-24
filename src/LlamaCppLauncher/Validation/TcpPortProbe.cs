using System.Net.NetworkInformation;

namespace LlamaCppLauncher.Validation;

public sealed class TcpPortProbe : ITcpPortProbe
{
    public bool IsPortListening(int port)
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        return properties.GetActiveTcpListeners().Any(endpoint => endpoint.Port == port);
    }
}
