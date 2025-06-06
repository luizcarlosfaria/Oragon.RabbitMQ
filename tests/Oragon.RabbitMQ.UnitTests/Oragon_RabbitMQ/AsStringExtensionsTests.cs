using System.Text;
using System.Collections.Generic;
using Oragon.RabbitMQ;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class AsStringExtensionsTests
{
    [Fact]
    public void AsString_Returns_Null_When_Key_Missing()
    {
        IDictionary<string, object> headers = new Dictionary<string, object>();
        string result = headers.AsString("missing");
        Assert.Null(result);
    }

    [Fact]
    public void AsString_Returns_Null_When_Dictionary_Null()
    {
        IDictionary<string, object> headers = null;
        string result = headers.AsString("missing");
        Assert.Null(result);
    }

    [Fact]
    public void AsString_Returns_String_From_ByteArray()
    {
        IDictionary<string, object> headers = new Dictionary<string, object>
        {
            {"data", Encoding.UTF8.GetBytes("hello")}
        };
        string result = headers.AsString("data");
        Assert.Equal("hello", result);
    }
}
