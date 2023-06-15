using Bunkum.CustomHttpListener.Listeners.Direct;
using Bunkum.HttpServer;
using JetBrains.Annotations;

namespace BunkumTests.HttpServer;

/*
 * Too much parallelization causes trouble, we don't need 220 threads trying to figure out if DummyUser is authed or not.
 * In the future, this should determine the amount of threads to get the most performance while not draining precious system resources,
 * especially on CI.
*/
public class ServerDependentTest
{
    [Pure]
    private protected (BunkumHttpServer, HttpClient) Setup(bool start = true)
    {
        DirectHttpListener listener = new();
        HttpClient client = listener.GetClient();

        BunkumHttpServer server = new(listener);
        server.AddAuthenticationService();
        if(start) server.Start(1);

        return (server, client);
    }
}