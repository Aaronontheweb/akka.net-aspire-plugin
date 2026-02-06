#nullable enable
using System;
using System.Net;
using Aaron.Akka.Discovery.Redis;
using Akka.Configuration;
using FluentAssertions;
using Xunit;

namespace Aaron.Akka.Discovery.Redis.Tests
{
    public class RedisDiscoverySettingsSpecs
    {
        [Fact]
        public void Create_should_parse_HOCON_configuration_correctly()
        {
            // Arrange
            var hocon = @"
                service-name = ""my-service""
                public-hostname = ""localhost""
                public-port = 9999
                connection-string = ""redis:6379""
                ttl = 5m
                ttl-heartbeat-interval = 45s
                key-prefix = ""test:prefix""
            ";
            var config = ConfigurationFactory.ParseString(hocon);

            // Act
            var settings = RedisDiscoverySettings.Create(config);

            // Assert
            settings.ServiceName.Should().Be("my-service");
            settings.HostName.Should().Be("localhost");
            settings.Port.Should().Be(9999);
            settings.ConnectionString.Should().Be("redis:6379");
            settings.Ttl.Should().Be(TimeSpan.FromMinutes(5));
            settings.TtlHeartbeatInterval.Should().Be(TimeSpan.FromSeconds(45));
            settings.KeyPrefix.Should().Be("test:prefix");
        }

        [Fact]
        public void Create_should_use_defaults_when_not_specified()
        {
            // Arrange
            var hocon = @"
                service-name = ""default""
                public-hostname = ""localhost""
                public-port = 8558
                connection-string = ""localhost""
                ttl = 2m
                ttl-heartbeat-interval = 30s
                key-prefix = ""akka:discovery""
            ";
            var config = ConfigurationFactory.ParseString(hocon);

            // Act
            var settings = RedisDiscoverySettings.Create(config);

            // Assert
            settings.ServiceName.Should().Be("default");
            settings.Ttl.Should().Be(TimeSpan.FromMinutes(2));
            settings.TtlHeartbeatInterval.Should().Be(TimeSpan.FromSeconds(30));
            settings.KeyPrefix.Should().Be("akka:discovery");
        }

        [Fact]
        public void WithServiceName_should_create_modified_copy()
        {
            // Arrange
            var original = RedisDiscoverySettings.Empty;

            // Act
            var modified = original.WithServiceName("new-service");

            // Assert
            modified.ServiceName.Should().Be("new-service");
            modified.HostName.Should().Be(original.HostName);
            modified.Port.Should().Be(original.Port);
            original.ServiceName.Should().Be("default"); // Original should not change
        }

        [Fact]
        public void WithPort_should_create_modified_copy()
        {
            // Arrange
            var original = RedisDiscoverySettings.Empty;

            // Act
            var modified = original.WithPort(9999);

            // Assert
            modified.Port.Should().Be(9999);
            modified.ServiceName.Should().Be(original.ServiceName);
            original.Port.Should().Be(8558); // Original should not change
        }

        [Fact]
        public void WithTtl_should_create_modified_copy()
        {
            // Arrange
            var original = RedisDiscoverySettings.Empty;

            // Act
            var modified = original.WithTtl(TimeSpan.FromMinutes(10));

            // Assert
            modified.Ttl.Should().Be(TimeSpan.FromMinutes(10));
            modified.ServiceName.Should().Be(original.ServiceName);
            original.Ttl.Should().Be(TimeSpan.FromMinutes(2)); // Original should not change
        }

        [Fact]
        public void Constructor_should_throw_when_port_is_invalid()
        {
            // Act & Assert
            var act = () => RedisDiscoverySettings.Empty.WithPort(0);
            act.Should().Throw<ArgumentException>();

            act = () => RedisDiscoverySettings.Empty.WithPort(70000);
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Constructor_should_throw_when_ttl_heartbeat_interval_exceeds_ttl()
        {
            // Act & Assert
            var act = () => RedisDiscoverySettings.Empty
                .WithTtl(TimeSpan.FromSeconds(30))
                .WithTtlHeartbeatInterval(TimeSpan.FromSeconds(45));

            act.Should().Throw<ArgumentException>();
        }
    }
}
