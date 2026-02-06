using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Aaron.Akka.Aspire.Tests;

public class AkkaAspireClusterSettingsSpecs
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var settings = new AkkaAspireClusterSettings();

        // Assert
        settings.Enabled.Should().BeFalse();
        settings.RemotePort.Should().Be(8081);
        settings.ManagementPort.Should().Be(8558);
        settings.PublicHostName.Should().Be("localhost");
        settings.ServiceName.Should().Be("default");
        settings.RequiredContactPointsNr.Should().Be(1);
        settings.FilterOnFallbackPort.Should().BeFalse();
        settings.Clustering.Should().BeNull();
    }

    [Fact]
    public void BindFromConfiguration_WithCustomValues_ShouldPopulateSettings()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:RemotePort", "9999" },
            { "Akka:Cluster:ManagementPort", "7777" },
            { "Akka:Cluster:PublicHostName", "myhost.example.com" },
            { "Akka:Cluster:ServiceName", "my-service" },
            { "Akka:Cluster:RequiredContactPointsNr", "3" },
            { "Akka:Cluster:FilterOnFallbackPort", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();

        // Act
        configuration.GetSection("Akka:Cluster").Bind(settings);

        // Assert
        settings.Enabled.Should().BeTrue();
        settings.RemotePort.Should().Be(9999);
        settings.ManagementPort.Should().Be(7777);
        settings.PublicHostName.Should().Be("myhost.example.com");
        settings.ServiceName.Should().Be("my-service");
        settings.RequiredContactPointsNr.Should().Be(3);
        settings.FilterOnFallbackPort.Should().BeTrue();
    }

    [Fact]
    public void BindFromConfiguration_WithClusteringSection_ShouldBindProperly()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:Clustering:ProviderType", "Redis" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();

        // Act
        configuration.GetSection("Akka:Cluster").Bind(settings);

        // Assert
        settings.Enabled.Should().BeTrue();
        settings.Clustering.Should().NotBeNull();
        settings.Clustering!.ProviderType.Should().Be("Redis");
    }

    [Fact]
    public void BindFromConfiguration_WithPartialConfiguration_ShouldUseDefaults()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:ServiceName", "test-service" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var settings = new AkkaAspireClusterSettings();

        // Act
        configuration.GetSection("Akka:Cluster").Bind(settings);

        // Assert
        settings.Enabled.Should().BeTrue();
        settings.ServiceName.Should().Be("test-service");
        settings.RemotePort.Should().Be(8081); // default
        settings.ManagementPort.Should().Be(8558); // default
        settings.PublicHostName.Should().Be("localhost"); // default
    }
}
