using System.Text.Json;
using LogViewerApi.Models;
using Xunit;

namespace LogViewerApi.Tests;

public class ResponseModelSerializationTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public void ProjectInfo_SerializesToSnakeCaseWithLastModified()
    {
        var timestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var project = new ProjectInfo("my-project", timestamp);

        var json = JsonSerializer.Serialize(project, SnakeCaseOptions);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("id", out var idProp));
        Assert.Equal("my-project", idProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("last_modified", out var lastModProp));
        Assert.Contains("2026-01-15", lastModProp.GetString());
    }

    [Fact]
    public void ProjectListResponse_SerializesToSnakeCaseWithNestedProjects()
    {
        var projects = new List<ProjectInfo>
        {
            new("project-alpha", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new("project-beta", new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero))
        };
        var response = new ProjectListResponse(projects);

        var json = JsonSerializer.Serialize(response, SnakeCaseOptions);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("projects", out var projectsProp));
        Assert.Equal(JsonValueKind.Array, projectsProp.ValueKind);
        Assert.Equal(2, projectsProp.GetArrayLength());

        var first = projectsProp[0];
        Assert.True(first.TryGetProperty("id", out var firstId));
        Assert.Equal("project-alpha", firstId.GetString());
        Assert.True(first.TryGetProperty("last_modified", out _));
    }

    [Fact]
    public void RunInfo_SerializesToSnakeCaseWithLastModified()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 14, 0, 0, TimeSpan.Zero);
        var run = new RunInfo("20260301-140000", timestamp);

        var json = JsonSerializer.Serialize(run, SnakeCaseOptions);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("id", out var idProp));
        Assert.Equal("20260301-140000", idProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("last_modified", out var lastModProp));
        Assert.Contains("2026-03-01", lastModProp.GetString());
    }

    [Fact]
    public void RunListResponse_SerializesToSnakeCaseWithProjectIdAndRuns()
    {
        var runs = new List<RunInfo>
        {
            new("20260301-100000", new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero)),
            new("20260301-120000", new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero))
        };
        var response = new RunListResponse("my-project", runs);

        var json = JsonSerializer.Serialize(response, SnakeCaseOptions);
        var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectIdProp));
        Assert.Equal("my-project", projectIdProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("runs", out var runsProp));
        Assert.Equal(JsonValueKind.Array, runsProp.ValueKind);
        Assert.Equal(2, runsProp.GetArrayLength());

        var first = runsProp[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("last_modified", out _));
    }
}
