using System.Net;
using Bunkum.HttpServer;
using Bunkum.HttpServer.Storage;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class StorageIntegrationTests : ServerDependentTest
{
    [Test]
    public async Task PutsAndGetsData()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<StorageEndpoints>();
        server.AddStorageService<InMemoryDataStore>();
        
        HttpResponseMessage msg = await client.GetAsync("/storage/put");
        Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        msg = await client.GetAsync("/storage/get");
        this.TearDown(server);
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("data"));
        });
    }
}