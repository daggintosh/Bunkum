using System.Net;
using Bunkum.HttpServer.Storage;
using NotEnoughLogs;

namespace Bunkum.HttpServer;

public struct RequestContext
{
    public HttpListenerRequest Request;
    public LoggerContainer<BunkumContext> Logger;
    public IDataStore DataStore;
}