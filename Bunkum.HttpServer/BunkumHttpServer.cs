using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using Bunkum.CustomHttpListener;
using Bunkum.CustomHttpListener.Listeners;
using Bunkum.CustomHttpListener.Parsing;
using Bunkum.CustomHttpListener.Request;
using Bunkum.HttpServer.Configuration;
using Bunkum.HttpServer.Database;
using Bunkum.HttpServer.Database.Dummy;
using Bunkum.HttpServer.Endpoints;
using Bunkum.HttpServer.Endpoints.Middlewares;
using Bunkum.HttpServer.Services;
using NotEnoughLogs;
using NotEnoughLogs.Loggers;

namespace Bunkum.HttpServer;

/// <summary>
/// The main class representing a Bunkum HTTP server and it's configuration.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
public partial class BunkumHttpServer
{
    private readonly BunkumHttpListener _listener;
    private readonly List<EndpointGroup> _endpoints = new();
    private readonly LoggerContainer<BunkumContext> _logger;
    
    private IDatabaseProvider<IDatabaseContext> _databaseProvider = new DummyDatabaseProvider();
    
    private readonly List<Config> _configs;

    private readonly List<IMiddleware> _middlewares = new();
    private readonly List<Service> _services = new();

    public bool started = false;
    private BunkumHttpServer(bool setListener, bool logToConsole)
    {
        this._logger = new LoggerContainer<BunkumContext>();
        if(logToConsole) this._logger.RegisterLogger(new ConsoleLogger());
        
        this._logger.LogInfo(BunkumContext.Startup, $"Bunkum is storing its data at {BunkumFileSystem.DataDirectory}.");
        if (!BunkumFileSystem.UsingCustomDirectory)
        {
            this._logger.LogInfo(BunkumContext.Startup, "You can override where data is stored using the BUNKUM_DATA_FOLDER environment variable.");
        }

        BunkumConfig bunkumConfig = Config.LoadFromFile<BunkumConfig>("bunkum.json", this._logger);
        this._configs = new List<Config>(2)
        {
            bunkumConfig,
        };

        if (setListener)
        {
            Uri listenEndpoint = new($"http://{bunkumConfig.ListenHost}:{bunkumConfig.ListenPort}");
            this._listener = new SocketHttpListener(listenEndpoint, bunkumConfig.UseForwardedIp);
        }
        else
        {
            this._listener = null!;
        }
    }

    public BunkumHttpServer() : this(true, true) {}
    
    public BunkumHttpServer(BunkumHttpListener listener, bool logToConsole = true) : this(false, logToConsole)
    {
        this._listener = listener;
    }

    [Obsolete("This method of creating the server will not let the user configure the endpoint or it's properties.")]
    public BunkumHttpServer(Uri listenEndpoint) : this(new SocketHttpListener(listenEndpoint, false))
    {
        this._logger.LogDebug(BunkumContext.Startup, $"Using hardcoded listen endpoint {listenEndpoint}");
        this._logger.LogDebug(BunkumContext.Startup, "Forwarded IP will be ignored - this method is not advised");
    }

    /// <summary>
    /// Start the server in multithreaded mode. Caller is responsible for blocking.
    /// </summary>
    /// <param name="taskOverride">Override the number of tasks spun up.</param>
    public void Start(int? taskOverride = null)
    {
        this.RunStartupTasks();

        BunkumConfig? bunkumConfig = (BunkumConfig?)this._configs.FirstOrDefault(c => c is BunkumConfig);
        Debug.Assert(bunkumConfig != null);

        int tasks = taskOverride ?? bunkumConfig.ThreadCount;

        this._logger.LogInfo(BunkumContext.Startup, $"Blocking in multithreaded mode with {tasks} tasks");

        for (int i = 0; i < tasks; i++)
        {
            int threadN = i + 1;
            Task.Factory.StartNew(async () =>
            {
                this._logger.LogTrace(BunkumContext.Startup, $"Spinning up task {threadN}/{tasks}");
                await this.Block();
            });
        }
    }

    /// <summary>
    /// Start the server in single-threaded mode. Bunkum is responsible for blocking.
    /// </summary>
    public async Task StartAndBlockAsync()
    {
        this.RunStartupTasks();
        this._logger.LogInfo(BunkumContext.Startup, "Blocking in single-threaded mode");
        await this.Block();
    }

    private void RunStartupTasks()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        
        this._logger.LogInfo(BunkumContext.Startup, "Starting up...");

        foreach (Service service in this._services)
        {
            this._logger.LogInfo(BunkumContext.Startup, $"Initializing service {service.GetType().Name}...");
            service.Initialize();
        }

        if (this._services.Count > 0)
        {
            string was = this._services.Count == 1 ? " was" : "s were";
            this._logger.LogInfo(BunkumContext.Startup, $"{this._services.Count} service{was} initialized.");
        }

        this._logger.LogDebug(BunkumContext.Startup, "Initializing database provider...");
        this._databaseProvider.Initialize();
        
        this._logger.LogDebug(BunkumContext.Startup, "Starting listener...");
        try
        {
            this._listener.StartListening();
        }
        catch(Exception e)
        {
            this._logger.LogCritical(BunkumContext.Startup, $"An exception occured while trying to start the listener: \n{e}");
            this._logger.LogCritical(BunkumContext.Startup, "Visit this page to view troubleshooting steps: " +
                                                             "https://littlebigrefresh.github.io/Docs/refresh-troubleshooting");
            
            this._logger.Dispose();
            BunkumConsole.WaitForInputAndExit(1);
        }
        
        this._logger.LogDebug(BunkumContext.Startup, "Warming up database provider...");
        this._databaseProvider.Warmup();

        stopwatch.Stop();
        this._logger.LogInfo(BunkumContext.Startup, $"Ready to go! Startup tasks took {stopwatch.ElapsedMilliseconds}ms.");
    }

    //[DoesNotReturn]
    private async Task Block()
    {
        this.started = true;
        while (this.started)
            await this._listener.WaitForConnectionAsync(async context => await Task.Factory.StartNew(async () =>
            {
                try
                {
                    // Create a new lazy to get a database context, if the value is never accessed, a database instance is never passed
                    Lazy<IDatabaseContext> database = new(this._databaseProvider.GetContext());

                    // Handle the request
                    await this.HandleRequest(context, database);

                    if (database.IsValueCreated)
                        database.Value.Dispose();
                }
                catch (Exception e)
                {
                    this._logger.LogError(BunkumContext.Request, $"Failed to initialize request:\n{e}");
                    context.ResponseCode = HttpStatusCode.InternalServerError;
                    #if DEBUG
                    context.Write(e.ToString());
                    #else
                    context.Write("Internal Server Error");
                    #endif
                }
            }));
        // ReSharper disable once FunctionNeverReturns
    }

    private async Task HandleRequest(ListenerContext context, Lazy<IDatabaseContext> database)
    {
        Stopwatch requestStopwatch = new();
        requestStopwatch.Start();

        try
        {
            // if (this.UseDigestSystem) this.VerifyDigestRequest(context, context.InputStream);
            Debug.Assert(context.InputStream.Position == 0); // should be at position 0 before we pass to the middleware chain
            
            // Setup a base middleware that calls Endpoints.
            // Passing in these parameters is a little janky in my opinion, but it gets the job done. 
            MainMiddleware mainMiddleware = new(this._endpoints,
                this._logger,
                this._services,
                this._configs);

            Action next = () => { mainMiddleware.HandleRequest(context, database, null!); };
            
            foreach (IMiddleware middleware in this._middlewares)
            {
                // Without this copy, next won't be the same when we invoke next() in the middleware
                // By creating the copy, we ensure the pipeline stays in order.
                // https://www.jetbrains.com/help/rider/AccessToModifiedClosure.html
                Action nextCopy = next;
                
                // Every middleware triggers the last when next() is called.
                // For example:
                //   server.AddMiddleware<MiddlewareA>();
                //   server.AddMiddleware<MiddlewareB>();
                // results in:
                //   MiddlewareB -> MiddlewareA -> mainMiddleware
                // since adding MiddlewareB encapsulates the previous middleware, MiddlewareA.
                //
                // It's important to note that middlewares can "halt" this chain by simply not calling next().
                // This is by design.
                next = () => { middleware.HandleRequest(context, database, nextCopy); };
            }

            next(); // Trigger the pipeline
        }
        catch (Exception e)
        {
            this._logger.LogError(BunkumContext.Request, e.ToString());

            try
            {
                context.ResponseType = ContentType.Plaintext;
                context.ResponseCode = HttpStatusCode.InternalServerError;

#if DEBUG
                context.Write(e.ToString());
#else
                context.Write("Internal Server Error");
#endif
            }
            catch
            {
                if (Debugger.IsAttached) throw;
            }
        }
        finally
        {
            try
            {
                requestStopwatch.Stop();

                this._logger.LogInfo(BunkumContext.Request, $"Served request to {context.RemoteEndpoint}: " +
                                                          $"{(int)context.ResponseCode} {context.ResponseCode} on " +
                                                          $"{context.Method.ToString().ToUpper()} '{context.Uri.PathAndQuery}' " +
                                                          $"({requestStopwatch.ElapsedMilliseconds}ms)");

                await context.FlushResponseAndClose();
            }
            catch
            {
                // ignored
            }
        }
    }

    private void AddEndpointGroup(Type type)
    {
        EndpointGroup? doc = (EndpointGroup?)Activator.CreateInstance(type);
        Debug.Assert(doc != null);
        
        this._endpoints.Add(doc);
    }

    public void AddEndpointGroup<TDoc>() where TDoc : EndpointGroup => this.AddEndpointGroup(typeof(TDoc));

    public void DiscoverEndpointsFromAssembly(Assembly assembly)
    {
        List<Type> types = assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(EndpointGroup)))
            .ToList();

        foreach (Type type in types) this.AddEndpointGroup(type);
    }

    // ReSharper disable UnusedMember.Global
    // ReSharper disable MemberCanBePrivate.Global
    public void UseDatabaseProvider(IDatabaseProvider<IDatabaseContext> provider)
    {
        this._databaseProvider = provider;
    }
    
    public void UseConfig(Config config)
    {
        this._configs.Add(config);
    }

    // TODO: Configuration hot reload
    // TODO: .ini? would be helpful as it supports comments and we can document in the file itself
    /// <summary>
    /// Defines a <see cref="Config"/> that is passed down to your endpoints.
    /// </summary>
    /// <param name="filename">What the config's filename should be stored as</param>
    /// <typeparam name="TConfig">An object extending <see cref="Config"/> that represents your server's configuration.</typeparam>
    public void UseJsonConfig<TConfig>(string filename) where TConfig : Config, new()
    {
        TConfig config = Config.LoadFromFile<TConfig>(filename, this._logger);
        this.UseConfig(config);
    }

    public void AddMiddleware<TMiddleware>() where TMiddleware : IMiddleware, new() => this.AddMiddleware(new TMiddleware());
    public void AddMiddleware<TMiddleware>(TMiddleware middleware) where TMiddleware : IMiddleware => this._middlewares.Add(middleware);
}