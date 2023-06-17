using Bunkum.CustomHttpListener.Listeners.Direct;
using Bunkum.HttpServer;
using JetBrains.Annotations;

namespace BunkumTests.HttpServer;

[Parallelizable]
public class ServerDependentTest
{
    [Pure]
    private protected (BunkumHttpServer, HttpClient) Setup(bool start = true)
    {
        DirectHttpListener listener = new();
        HttpClient client = listener.GetClient();

        BunkumHttpServer server = new(listener);
        server.AddAuthenticationService();
        if(start) server.Start();

        return (server, client);
    }
    private protected void TearDown(BunkumHttpServer server)
    {
        GC.Collect();
        server.cts.Cancel();
        server.cts.Dispose();
    }
}