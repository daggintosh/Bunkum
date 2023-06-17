using System.Net;
using System.Text;
using System.Xml.Serialization;
using Bunkum.HttpServer;
using BunkumTests.HttpServer.Endpoints;
using Newtonsoft.Json;

namespace BunkumTests.HttpServer.Tests;

public class ResponseTests : ServerDependentTest
{
    [Test]
    [TestCase("/response/string", HttpStatusCode.OK)]
    [TestCase("/response/responseObject", HttpStatusCode.OK)]
    [TestCase("/response/responseObjectWithCode", HttpStatusCode.Accepted)]
    public void CorrectResponseForAllTypes(string endpoint, HttpStatusCode codeToCheckFor)
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<ResponseEndpoints>();
        
        HttpResponseMessage msg = client.Send(new HttpRequestMessage(HttpMethod.Get, endpoint));
        this.TearDown(server);
        Assert.Multiple(async () =>
        {
            Assert.That(await msg.Content.ReadAsStringAsync(), Is.EqualTo("works"));
            Assert.That(msg.StatusCode, Is.EqualTo(codeToCheckFor));
        });
    }

    [Test]
    public async Task CorrectResponseForSerializedXml()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<ResponseEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/response/serializedXml"));
        string text = await msg.Content.ReadAsStringAsync();
        
        Assert.That(text, Is.EqualTo("<responseSerializedObject><value>69</value></responseSerializedObject>"));
        
        // technically we probably dont need to test deserialization but we should anyways
        XmlSerializer serializer = new(typeof(ResponseSerializationObject));

        ResponseSerializationObject? obj = (ResponseSerializationObject?)serializer.Deserialize(new MemoryStream(Encoding.Default.GetBytes(text)));
        this.TearDown(server);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.Value, Is.EqualTo(69));
    }
    
    [Test]
    public async Task CorrectResponseForSerializedJson()
    {
        (BunkumHttpServer server, HttpClient client) = this.Setup();
        server.AddEndpointGroup<ResponseEndpoints>();
        
        HttpResponseMessage msg = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/response/serializedJson"));
        string text = await msg.Content.ReadAsStringAsync();
        
        Assert.That(text, Is.EqualTo("{\"value\":69}"));
        
        // technically we probably dont need to test deserialization but we should anyways
        ResponseSerializationObject? obj = JsonConvert.DeserializeObject<ResponseSerializationObject>(text);
        
        this.TearDown(server);
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj!.Value, Is.EqualTo(69));
    }
}