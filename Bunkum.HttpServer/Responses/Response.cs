using System.Net;
using System.Text;
using System.Xml.Serialization;
using Bunkum.CustomHttpListener.Parsing;
using Bunkum.HttpServer.Serialization;
using Newtonsoft.Json;

namespace Bunkum.HttpServer.Responses;

public partial struct Response
{
    public Response(byte[] data, ContentType contentType = ContentType.Html, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        this.StatusCode = statusCode;
        this.Data = data;
        this.ContentType = contentType;
    }

    #region Serialization setup
    private static readonly XmlSerializerNamespaces Namespaces = new();
    private static readonly JsonSerializer _jsonSerializer = new();

    static Response()
    {
        Namespaces.Add("", "");
        SetupResponseCache();
    }
    #endregion

    private static partial void SetupResponseCache();

    public Response(HttpStatusCode statusCode) : this("", ContentType.BinaryData, statusCode) 
    {}

    public Response(object? data, ContentType contentType = ContentType.Html, HttpStatusCode statusCode = HttpStatusCode.OK, bool skipSerialization = false)
    {
        this.ContentType = contentType;
        this.StatusCode = statusCode;

        if (skipSerialization || data is null or string || !contentType.IsSerializable())
        {
            this.Data = Encoding.Default.GetBytes(data?.ToString() ?? string.Empty);
            return;
        }
        
        if(data is INeedsPreparationBeforeSerialization prep) 
            prep.PrepareForSerialization();

        using MemoryStream stream = new();
        switch (contentType)
        {
            case ContentType.Html:
            case ContentType.Plaintext:
            case ContentType.BinaryData:
                throw new InvalidOperationException();
            case ContentType.Xml:
            {
                using BunkumXmlTextWriter writer = new(stream);

                XmlSerializer serializer = new(data.GetType());
                serializer.Serialize(writer, data, Namespaces);
                break;
            }
            case ContentType.Json:
            {
                using StreamWriter sw = new(stream);
                using JsonWriter writer = new JsonTextWriter(sw);
                
                _jsonSerializer.Serialize(writer, data);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(contentType), contentType, null);
        }

        this.Data = stream.ToArray();
    }

    public readonly HttpStatusCode StatusCode;
    public readonly ContentType ContentType;
    public readonly byte[] Data;
}