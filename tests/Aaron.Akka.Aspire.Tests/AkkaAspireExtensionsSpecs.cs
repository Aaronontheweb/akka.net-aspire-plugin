using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Aaron.Akka.Aspire.Tests;

public class AkkaAspireExtensionsSpecs
{
    [Fact]
    public void WithAspireClusterBootstrap_WhenDisabled_ShouldBeNoOp()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        // Act - should not throw
        var result = builder.WithAspireClusterBootstrap(sp);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithRedisProvider_ShouldConfigureCorrectHocon()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:RemotePort", "8081" },
            { "Akka:Cluster:ManagementPort", "8558" },
            { "Akka:Cluster:PublicHostName", "localhost" },
            { "Akka:Cluster:ServiceName", "test-service" },
            { "Akka:Cluster:RequiredContactPointsNr", "2" },
            { "Akka:Cluster:FilterOnFallbackPort", "false" },
            { "Akka:Cluster:Clustering:ProviderType", "Redis" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        // Act
        builder.WithAspireClusterBootstrap(sp);

        // Build the actor system to verify HOCON configuration
        var host = new HostBuilder()
            .ConfigureServices((context, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();

        // Assert - verify key HOCON values are set correctly
        var config = actorSystem.Settings.Config;

        // Verify discovery method is set to redis
        config.GetString("akka.discovery.method").Should().Be("redis");

        // Verify remote configuration
        config.GetString("akka.remote.dot-netty.tcp.hostname").Should().Be("0.0.0.0");
        config.GetInt("akka.remote.dot-netty.tcp.port").Should().Be(8081);
        config.GetString("akka.remote.dot-netty.tcp.public-hostname").Should().Be("localhost");
        config.GetInt("akka.remote.dot-netty.tcp.public-port").Should().Be(8081);

        // Verify management configuration
        config.GetString("akka.management.http.hostname").Should().Be("localhost");
        config.GetInt("akka.management.http.port").Should().Be(8558);

        // Verify cluster bootstrap configuration
        config.GetInt("akka.management.cluster.bootstrap.contact-point-discovery.required-contact-point-nr").Should().Be(2);
        config.GetString("akka.management.cluster.bootstrap.contact-point-discovery.service-name").Should().Be("test-service");
        config.GetBoolean("akka.management.cluster.bootstrap.contact-point.filter-on-fallback-port").Should().BeFalse();

        // Verify discovery plugin hostname/port injection
        config.GetString("akka.discovery.redis.public-hostname").Should().Be("localhost");
        config.GetInt("akka.discovery.redis.public-port").Should().Be(8558);

        // Cleanup
        host.Dispose();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithAzureProvider_ShouldSetAzureDiscoveryMethod()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:Clustering:ProviderType", "AzureTableStorage" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        // Act
        builder.WithAspireClusterBootstrap(sp);

        // Build the actor system to verify HOCON configuration
        var host = new HostBuilder()
            .ConfigureServices((context, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();

        // Assert
        var config = actorSystem.Settings.Config;
        config.GetString("akka.discovery.method").Should().Be("azure");

        // Cleanup
        host.Dispose();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithKubernetesProvider_ShouldSetKubernetesDiscoveryMethod()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:Clustering:ProviderType", "Kubernetes" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        // Act
        builder.WithAspireClusterBootstrap(sp);

        // Build the actor system to verify HOCON configuration
        var host = new HostBuilder()
            .ConfigureServices((context, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();

        // Assert
        var config = actorSystem.Settings.Config;
        config.GetString("akka.discovery.method").Should().Be("kubernetes-api");

        // Cleanup
        host.Dispose();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenEnabledWithNoProvider_ShouldDefaultToConfig()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        // Act
        builder.WithAspireClusterBootstrap(sp);

        // Build the actor system to verify HOCON configuration
        var host = new HostBuilder()
            .ConfigureServices((context, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();

        // Assert
        var config = actorSystem.Settings.Config;
        config.GetString("akka.discovery.method").Should().Be("config");

        // Cleanup
        host.Dispose();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WithCustomClusterConfigure_ShouldApplyCallback()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;

        // Act
        builder.WithAspireClusterBootstrap(sp, clusterConfigure: clusterOptions =>
        {
            callbackInvoked = true;
            clusterOptions.Roles = new[] { "test-role" };
        });

        // Assert - verify callback was invoked
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WithDiscoveryCallback_ShouldInvokeCallback()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "true" },
            { "Akka:Cluster:Clustering:ProviderType", "Redis" },
            { "ConnectionStrings:akka-discovery", "localhost:6379" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;
        IConfiguration? receivedConfig = null;

        // Act
        builder.WithAspireClusterBootstrap(sp,
            configureDiscovery: (b, config) =>
            {
                callbackInvoked = true;
                receivedConfig = config;
            });

        // Assert
        callbackInvoked.Should().BeTrue();
        receivedConfig.Should().NotBeNull();
        receivedConfig!.GetConnectionString("akka-discovery").Should().Be("localhost:6379");
    }

    [Fact]
    public void WithAspireClusterBootstrap_WithNoDiscoveryCallback_ShouldStillSetDiscoveryMethod()
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

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        // Build the actor system to verify HOCON configuration
        var host = new HostBuilder()
            .ConfigureServices((context, serviceCollection) =>
            {
                serviceCollection.AddSingleton<IConfiguration>(configuration);
                serviceCollection.AddAkka("TestSystem", (akkaBuilder, provider) =>
                {
                    // No configureDiscovery callback - backward compat path
                    akkaBuilder.WithAspireClusterBootstrap(provider);
                });
            })
            .Build();

        var actorSystem = host.Services.GetRequiredService<ActorSystem>();

        // Assert - discovery method should still be set via HOCON fallback
        var config = actorSystem.Settings.Config;
        config.GetString("akka.discovery.method").Should().Be("redis");

        // Cleanup
        host.Dispose();
    }

    [Fact]
    public void WithAspireClusterBootstrap_WhenDisabled_ShouldNotInvokeDiscoveryCallback()
    {
        // Arrange
        var configDict = new Dictionary<string, string?>
        {
            { "Akka:Cluster:Enabled", "false" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        var sp = services.BuildServiceProvider();

        var builder = new AkkaConfigurationBuilder(services, "TestSystem");

        var callbackInvoked = false;

        // Act
        builder.WithAspireClusterBootstrap(sp,
            configureDiscovery: (b, config) =>
            {
                callbackInvoked = true;
            });

        // Assert - callback should NOT be invoked when clustering is disabled
        callbackInvoked.Should().BeFalse();
    }
}
