using System.Net;
using System.Net.Sockets;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Validation;

public class TcpPortProbeTests
{
    [Fact]
    public void IsPortListening_DetectsRealListeningSocket_AndFalseAfterStop()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var probe = new TcpPortProbe();

        try
        {
            Assert.True(probe.IsPortListening(port));
        }
        finally
        {
            listener.Stop();
        }

        Assert.False(probe.IsPortListening(port));
    }
}
