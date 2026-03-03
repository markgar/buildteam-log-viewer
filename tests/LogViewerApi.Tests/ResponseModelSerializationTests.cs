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
        using var doc = JsonDocument.Parse(json);

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
        using var doc = JsonDocument.Parse(json);

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
        using var doc = JsonDocument.Parse(json);

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
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectIdProp));
        Assert.Equal("my-project", projectIdProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("runs", out var runsProp));
        Assert.Equal(JsonValueKind.Array, runsProp.ValueKind);
        Assert.Equal(2, runsProp.GetArrayLength());

        var first = runsProp[0];
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("last_modified", out _));
    }

    [Fact]
    public void LogItemInfo_SerializesToSnakeCaseWithNameSizeAndLastModified()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 9, 15, 0, TimeSpan.Zero);
        var logItem = new LogItemInfo("build-output.log", 204800, timestamp);

        var json = JsonSerializer.Serialize(logItem, SnakeCaseOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("name", out var nameProp));
        Assert.Equal("build-output.log", nameProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("size", out var sizeProp));
        Assert.Equal(204800, sizeProp.GetInt64());

        Assert.True(doc.RootElement.TryGetProperty("last_modified", out var lastModProp));
        Assert.Contains("2026-03-01", lastModProp.GetString());
    }

    [Fact]
    public void LogListResponse_SerializesToSnakeCaseWithAllCategories()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        var logs = new List<LogItemInfo>
        {
            new("build.log", 1024, timestamp),
            new("deploy.log", 2048, timestamp)
        };
        var prompts = new List<LogItemInfo>
        {
            new("system-prompt.md", 512, timestamp)
        };
        var artifacts = new List<LogItemInfo>
        {
            new("output.zip", 1048576, timestamp)
        };
        var response = new LogListResponse("my-project", "20260301-100000", logs, prompts, artifacts);

        var json = JsonSerializer.Serialize(response, SnakeCaseOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectIdProp));
        Assert.Equal("my-project", projectIdProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("run_id", out var runIdProp));
        Assert.Equal("20260301-100000", runIdProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("logs", out var logsProp));
        Assert.Equal(2, logsProp.GetArrayLength());
        var firstLog = logsProp[0];
        Assert.True(firstLog.TryGetProperty("name", out var logName));
        Assert.Equal("build.log", logName.GetString());
        Assert.True(firstLog.TryGetProperty("size", out _));
        Assert.True(firstLog.TryGetProperty("last_modified", out _));

        Assert.True(doc.RootElement.TryGetProperty("prompts", out var promptsProp));
        Assert.Equal(1, promptsProp.GetArrayLength());

        Assert.True(doc.RootElement.TryGetProperty("artifacts", out var artifactsProp));
        Assert.Equal(1, artifactsProp.GetArrayLength());
    }

    [Fact]
    public void LogListResponse_WithEmptyCategories_SerializesToValidJson()
    {
        var response = new LogListResponse(
            "empty-project",
            "20260301-000000",
            new List<LogItemInfo>(),
            new List<LogItemInfo>(),
            new List<LogItemInfo>());

        var json = JsonSerializer.Serialize(response, SnakeCaseOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("project_id", out var projectIdProp));
        Assert.Equal("empty-project", projectIdProp.GetString());

        Assert.True(doc.RootElement.TryGetProperty("logs", out var logsProp));
        Assert.Equal(0, logsProp.GetArrayLength());

        Assert.True(doc.RootElement.TryGetProperty("prompts", out var promptsProp));
        Assert.Equal(0, promptsProp.GetArrayLength());

        Assert.True(doc.RootElement.TryGetProperty("artifacts", out var artifactsProp));
        Assert.Equal(0, artifactsProp.GetArrayLength());
    }

    [Fact]
    public void LogListResponse_WithMixedPopulatedAndEmptyCategories_SerializesCorrectly()
    {
        var timestamp = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var logs = new List<LogItemInfo>
        {
            new("agent.log", 4096, timestamp)
        };
        var response = new LogListResponse(
            "mixed-project",
            "20260301-120000",
            logs,
            new List<LogItemInfo>(),
            new List<LogItemInfo>());

        var json = JsonSerializer.Serialize(response, SnakeCaseOptions);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("logs", out var logsProp));
        Assert.Equal(1, logsProp.GetArrayLength());

        Assert.True(doc.RootElement.TryGetProperty("prompts", out var promptsProp));
        Assert.Equal(0, promptsProp.GetArrayLength());

        Assert.True(doc.RootElement.TryGetProperty("artifacts", out var artifactsProp));
        Assert.Equal(0, artifactsProp.GetArrayLength());
    }
}
