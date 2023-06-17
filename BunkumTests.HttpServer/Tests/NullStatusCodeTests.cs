using System.Net;
using Bunkum.HttpServer;
using BunkumTests.HttpServer.Endpoints;

namespace BunkumTests.HttpServer.Tests;

public class NullStatusCodeTests : ServerDependentTest
{
    [Test]
    public async Task ReturnsCorrectResponseWhenNull()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<NullEndpoints>();

        HttpResponseMessage resp = await client.GetAsync("/null?null=true");
        this.TearDown(server);
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
    
    [Test]
    public async Task ReturnsCorrectResponseWhenNotNull()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<NullEndpoints>();

        HttpResponseMessage resp = await client.GetAsync("/null?null=false");
        this.TearDown(server);
        Assert.That(resp.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}