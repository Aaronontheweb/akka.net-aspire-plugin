using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aaron.Akka.Aspire.Hosting.Tests;

/// <summary>
/// Tests for Akka.NET service extensions in .NET Aspire.
/// </summary>
public class AkkaServiceExtensionsSpecs
{
    [Fact]
    public void AddAkka_ShouldCreateAkkaServiceWithCorrectName()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var serviceName = "my-akka-service";

        // Act
        var akkaService = appBuilder.AddAkka(serviceName);

        // Assert
        akkaService.Should().NotBeNull();
        akkaService.Name.Should().Be(serviceName);
        akkaService.Builder.Should().BeSameAs(appBuilder);
        akkaService.Clustering.Should().BeNull();
    }

    [Fact]
    public void AddAkka_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        // Arrange
        IDistributedApplicationBuilder builder = null!;

        // Act
        var act = () => builder.AddAkka("test");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void AddAkka_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();

        // Act
        var act = () => appBuilder.AddAkka(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void WithClustering_ShouldSetClusteringProvider()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        var redis = appBuilder.AddRedis("redis");

        // Act
        var result = akkaService.WithClustering(redis);

        // Assert
        result.Should().BeSameAs(akkaService);
        akkaService.Clustering.Should().NotBeNull();
    }

    [Fact]
    public void WithClustering_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var redis = appBuilder.AddRedis("redis");
        AkkaService akkaService = null!;

        // Act
        var act = () => akkaService.WithClustering(redis);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("akkaService");
    }

    [Fact]
    public void WithClustering_WithNullResource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");

        // Act
        var act = () => akkaService.WithClustering(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resource");
    }

    [Fact]
    public void WithReference_ShouldConfigureEndpointsAndEnvironmentVariables()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var redis = appBuilder.AddRedis("redis");
        akkaService.WithClustering(redis);

        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Act
        var result = containerResource.WithReference(akkaService);

        // Assert
        result.Should().BeSameAs(containerResource);

        // Verify endpoints were added
        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>();
        endpoints.Should().Contain(e => e.Name == "akka-remote");
        endpoints.Should().Contain(e => e.Name == "akka-management");

        var remoteEndpoint = endpoints.First(e => e.Name == "akka-remote");
        remoteEndpoint.UriScheme.Should().Be("tcp");
        remoteEndpoint.IsProxied.Should().BeTrue();
        remoteEndpoint.IsExternal.Should().BeFalse();

        var managementEndpoint = endpoints.First(e => e.Name == "akka-management");
        managementEndpoint.UriScheme.Should().Be("http");
        managementEndpoint.IsProxied.Should().BeTrue();
        managementEndpoint.IsExternal.Should().BeFalse();

        // Verify environment callback was added
        var envCallbacks = containerResource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        envCallbacks.Should().NotBeEmpty();
    }

    [Fact]
    public void WithReference_WithoutClustering_ShouldStillConfigureEndpointsAndEnvironment()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Act
        var result = containerResource.WithReference(akkaService);

        // Assert
        result.Should().BeSameAs(containerResource);

        // Verify endpoints were added even without clustering
        var endpoints = containerResource.Resource.Annotations.OfType<EndpointAnnotation>();
        endpoints.Should().Contain(e => e.Name == "akka-remote");
        endpoints.Should().Contain(e => e.Name == "akka-management");
    }

    [Fact]
    public void WithReference_WithNullBuilder_ShouldThrowArgumentNullException()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-service");
        IResourceBuilder<ContainerResource> builder = null!;

        // Act
        var act = () => builder.WithReference(akkaService);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("builder");
    }

    [Fact]
    public void WithReference_WithNullAkkaService_ShouldThrowArgumentNullException()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Act
        var act = () => containerResource.WithReference(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("akkaService");
    }

    [Fact]
    public void WithReference_WithReplicas_ShouldSetCorrectRequiredContactPointsNr()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Manually add replica annotation since containers don't have WithReplicas
        containerResource.Resource.Annotations.Add(new ReplicaAnnotation(3));

        // Act
        containerResource.WithReference(akkaService);

        // Build the application to trigger environment callbacks
        using var app = appBuilder.Build();
        var distributedAppModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = distributedAppModel.Resources.OfType<ContainerResource>().First();

        // Get the environment variables by creating a context
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var envContext = new EnvironmentCallbackContext(executionContext);
        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        foreach (var callback in envCallbacks)
        {
            callback.Callback(envContext);
        }

        // Assert
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__RequiredContactPointsNr");
        envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"].Should().Be("3");
    }

    [Fact]
    public void WithReference_ShouldSetStandardEnvironmentVariables()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Act
        containerResource.WithReference(akkaService);

        // Build the application to trigger environment callbacks
        using var app = appBuilder.Build();
        var distributedAppModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = distributedAppModel.Resources.OfType<ContainerResource>().First();

        // Get the environment variables
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run);
        var envContext = new EnvironmentCallbackContext(executionContext);
        var envCallbacks = resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        foreach (var callback in envCallbacks)
        {
            callback.Callback(envContext);
        }

        // Assert
        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__Enabled");
        envContext.EnvironmentVariables["Akka__Cluster__Enabled"].Should().Be("true");

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__PublicHostName");
        envContext.EnvironmentVariables["Akka__Cluster__PublicHostName"].Should().Be("localhost");

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__ServiceName");
        envContext.EnvironmentVariables["Akka__Cluster__ServiceName"].Should().Be("my-akka-cluster");

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__FilterOnFallbackPort");
        envContext.EnvironmentVariables["Akka__Cluster__FilterOnFallbackPort"].Should().Be("false");

        envContext.EnvironmentVariables.Should().ContainKey("Akka__Cluster__RequiredContactPointsNr");
        envContext.EnvironmentVariables["Akka__Cluster__RequiredContactPointsNr"].Should().Be("1");
    }

    [Fact]
    public void ClusteringProvider_ShouldAddEnvironmentCallbacks()
    {
        // Arrange
        var appBuilder = DistributedApplication.CreateBuilder();
        var akkaService = appBuilder.AddAkka("my-akka-cluster");
        var redis = appBuilder.AddRedis("redis");
        akkaService.WithClustering(redis);

        var containerResource = appBuilder.AddContainer("test-container", "test-image");

        // Act
        containerResource.WithReference(akkaService);

        // Assert - verify environment callbacks were added by the clustering provider
        // The clustering provider calls WithReference and WithEnvironment, both of which add callbacks
        var envCallbacks = containerResource.Resource.Annotations.OfType<EnvironmentCallbackAnnotation>();
        envCallbacks.Should().NotBeEmpty("because clustering provider should add environment callbacks");
    }
}
