#nullable enable
using System;
using System.Threading.Tasks;
using Aaron.Akka.Discovery.Redis;
using Akka.Actor;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aaron.Akka.Discovery.Redis.Tests
{
    public class HostingSpecs
    {
        [Fact]
        public async Task WithRedisDiscovery_with_connection_string_should_generate_correct_config()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddAkka("TestSystem", builder =>
            {
                builder.WithRedisDiscovery("localhost:6379", "test-service");
            });

            // Assert - build the service provider to trigger configuration
            var provider = services.BuildServiceProvider();
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            actorSystem.Should().NotBeNull();

            // Configuration should have been applied
            var config = actorSystem.Settings.Config;
            config.GetString("akka.discovery.method").Should().Be("redis");
            config.GetString("akka.discovery.redis.connection-string").Should().Be("localhost:6379");
            config.GetString("akka.discovery.redis.service-name").Should().Be("test-service");

            // Clean up
            await actorSystem.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_with_options_action_should_generate_correct_config()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddAkka("TestSystem", builder =>
            {
                builder.WithRedisDiscovery(options =>
                {
                    options.ConnectionString = "redis-server:6379";
                    options.ServiceName = "my-cluster";
                    options.Port = 9999;
                    options.Ttl = TimeSpan.FromMinutes(5);
                });
            });

            // Assert
            var provider = services.BuildServiceProvider();
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            actorSystem.Should().NotBeNull();

            var config = actorSystem.Settings.Config;
            config.GetString("akka.discovery.method").Should().Be("redis");
            config.GetString("akka.discovery.redis.connection-string").Should().Be("redis-server:6379");
            config.GetString("akka.discovery.redis.service-name").Should().Be("my-cluster");
            config.GetInt("akka.discovery.redis.public-port").Should().Be(9999);
            config.GetTimeSpan("akka.discovery.redis.ttl").Should().Be(TimeSpan.FromMinutes(5));

            // Clean up
            await actorSystem.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_should_set_default_discovery_method()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddAkka("TestSystem", builder =>
            {
                builder.WithRedisDiscovery("localhost:6379");
            });

            // Assert
            var provider = services.BuildServiceProvider();
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            var config = actorSystem.Settings.Config;

            config.GetString("akka.discovery.method").Should().Be("redis");

            // Clean up
            await actorSystem.Terminate();
        }

        [Fact]
        public async Task WithRedisDiscovery_with_IsDefaultPlugin_false_should_not_set_default_method()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddAkka("TestSystem", builder =>
            {
                builder.WithRedisDiscovery(options =>
                {
                    options.ConnectionString = "localhost:6379";
                    options.IsDefaultPlugin = false;
                });
            });

            // Assert
            var provider = services.BuildServiceProvider();
            var actorSystem = provider.GetRequiredService<ActorSystem>();
            var config = actorSystem.Settings.Config;

            // The method should not be set to redis since IsDefaultPlugin is false
            config.HasPath("akka.discovery.method").Should().BeFalse();

            // Clean up
            await actorSystem.Terminate();
        }
    }
}
