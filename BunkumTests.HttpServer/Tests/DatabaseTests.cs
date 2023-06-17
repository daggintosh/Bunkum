using System.Net;
using Bunkum.HttpServer;
using BunkumTests.HttpServer.Database;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class DatabaseTests : ServerDependentTest
{
    [Test]
    public async Task PassesInDatabase()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<DatabaseEndpoints>();

        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/db/null"));
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("True"));
        });
    }
    
    [Test]
    public async Task GetsValueFromDatabase()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<DatabaseEndpoints>();

        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/db/value"));
        this.TearDown(server);
        Assert.Multiple(async () =>
        {
            Assert.That(msg.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("69"));
        });
    }
}