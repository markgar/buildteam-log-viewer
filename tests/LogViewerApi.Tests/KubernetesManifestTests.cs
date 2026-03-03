using Xunit;

namespace LogViewerApi.Tests;

public class KubernetesManifestTests
{
    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "LogViewerApi.sln")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? throw new InvalidOperationException("Could not find repository root");
    }

    private static string ReadManifest(string relativePath)
    {
        var repoRoot = FindRepoRoot();
        var fullPath = Path.Combine(repoRoot, relativePath);
        Assert.True(File.Exists(fullPath), $"Expected manifest at {relativePath}");
        return File.ReadAllText(fullPath);
    }

    [Fact]
    public void DeploymentManifest_HasRequiredDeploymentStructure()
    {
        var yaml = ReadManifest("k8s/deployment.yaml");

        Assert.Contains("apiVersion: apps/v1", yaml);
        Assert.Contains("kind: Deployment", yaml);
        Assert.Contains("name: log-viewer-api", yaml);
        Assert.Contains("replicas:", yaml);
    }

    [Fact]
    public void DeploymentManifest_IsConfiguredForAzureWorkloadIdentity()
    {
        var yaml = ReadManifest("k8s/deployment.yaml");

        Assert.Contains("azure.workload.identity/use: \"true\"", yaml);
        Assert.Contains("serviceAccountName: buildteam-sa", yaml);
    }

    [Fact]
    public void DeploymentManifest_HasHealthProbes()
    {
        var yaml = ReadManifest("k8s/deployment.yaml");

        Assert.Contains("livenessProbe:", yaml);
        Assert.Contains("readinessProbe:", yaml);
        Assert.Contains("path: /health", yaml);
        Assert.Contains("port: 8080", yaml);
    }

    [Fact]
    public void ServiceManifest_ExposesPort80TargetingContainerPort8080()
    {
        var yaml = ReadManifest("k8s/service.yaml");

        Assert.Contains("apiVersion: v1", yaml);
        Assert.Contains("kind: Service", yaml);
        Assert.Contains("name: log-viewer-api", yaml);
        Assert.Contains("port: 80", yaml);
        Assert.Contains("targetPort: 8080", yaml);
        Assert.Contains("type: ClusterIP", yaml);
    }

    [Fact]
    public void DeploymentAndService_ShareMatchingSelectorLabels()
    {
        var deployment = ReadManifest("k8s/deployment.yaml");
        var service = ReadManifest("k8s/service.yaml");

        // Both should select on app: log-viewer-api
        Assert.Contains("app: log-viewer-api", deployment);
        Assert.Contains("app: log-viewer-api", service);
    }
}
