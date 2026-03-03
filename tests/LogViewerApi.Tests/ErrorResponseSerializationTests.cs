using System.Text.Json;
using LogViewerApi.Models;
using Xunit;

namespace LogViewerApi.Tests;

public class ErrorResponseSerializationTests
{
    [Fact]
    public void ErrorResponse_SerializesToSnakeCaseJsonEnvelope()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var response = new ErrorResponse("Something went wrong");
        var json = JsonSerializer.Serialize(response, options);

        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("Something went wrong", errorProp.GetString());
    }
}
