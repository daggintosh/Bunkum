using Bunkum.CustomHttpListener.Listeners;
using Bunkum.CustomHttpListener.Listeners.Direct;
using Bunkum.HttpServer;
using JetBrains.Annotations;

namespace BunkumTests.HttpServer;

/*
 * Too much parallelization causes trouble, we don't need 220 threads trying to figure out if Dummy is authed or not
 * In the future, this should determine the amount of viable threads for doing tests,
 * we need to fit the tests into a more sane amount of threads
*/
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
        if(start) server.Start(1);

        return (server, client);
    }
}