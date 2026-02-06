#nullable enable
using System;
using System.Net;
using Akka.Actor;
using Akka.Configuration;

namespace Aaron.Akka.Discovery.Redis
{
    /// <summary>
    /// Settings for the Redis-based service discovery plugin
    /// </summary>
    public sealed class RedisDiscoverySettings
    {
        /// <summary>
        /// Default empty settings
        /// </summary>
        public static readonly RedisDiscoverySettings Empty = new RedisDiscoverySettings(
            serviceName: "default",
            hostName: Dns.GetHostName(),
            port: 8558,
            connectionString: "<connection-string>",
            ttl: TimeSpan.FromMinutes(2),
            ttlHeartbeatInterval: TimeSpan.FromSeconds(30),
            keyPrefix: "akka:discovery");

        /// <summary>
        /// Creates settings from the actor system configuration
        /// </summary>
        /// <param name="system">The actor system</param>
        /// <returns>Settings instance</returns>
        public static RedisDiscoverySettings Create(ActorSystem system)
            => Create(system.Settings.Config.GetConfig("akka.discovery.redis"));

        /// <summary>
        /// Creates settings from a configuration object
        /// </summary>
        /// <param name="config">The configuration to read from</param>
        /// <returns>Settings instance</returns>
        public static RedisDiscoverySettings Create(Config config)
        {
            var host = config.GetString("public-hostname");
            if (string.IsNullOrWhiteSpace(host))
                host = Dns.GetHostName();

            return new RedisDiscoverySettings(
                serviceName: config.GetString("service-name"),
                hostName: host,
                port: config.GetInt("public-port"),
                connectionString: config.GetString("connection-string"),
                ttl: config.GetTimeSpan("ttl"),
                ttlHeartbeatInterval: config.GetTimeSpan("ttl-heartbeat-interval"),
                keyPrefix: config.GetString("key-prefix"));
        }

        private RedisDiscoverySettings(
            string serviceName,
            string hostName,
            int port,
            string connectionString,
            TimeSpan ttl,
            TimeSpan ttlHeartbeatInterval,
            string keyPrefix)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("Must not be empty or whitespace", nameof(serviceName));

            if (string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException("Must not be empty or whitespace", nameof(hostName));

            if (port < 1 || port > 65535)
                throw new ArgumentException("Must be greater than zero and less than or equal to 65535", nameof(port));

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Must not be empty or whitespace", nameof(connectionString));

            if (ttl <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttl));

            if (ttlHeartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentException("Must be greater than zero", nameof(ttlHeartbeatInterval));

            if (ttlHeartbeatInterval >= ttl)
                throw new ArgumentException("Must be less than ttl", nameof(ttlHeartbeatInterval));

            if (string.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("Must not be empty or whitespace", nameof(keyPrefix));

            ServiceName = serviceName;
            HostName = hostName;
            Port = port;
            ConnectionString = connectionString;
            Ttl = ttl;
            TtlHeartbeatInterval = ttlHeartbeatInterval;
            KeyPrefix = keyPrefix;
        }

        /// <summary>
        /// The service name assigned to the cluster
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// The public facing hostname of this node
        /// </summary>
        public string HostName { get; }

        /// <summary>
        /// The public open akka management port of this node
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// The connection string used to connect to Redis
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// The time-to-live for Redis keys
        /// </summary>
        public TimeSpan Ttl { get; }

        /// <summary>
        /// The time-to-live heartbeat update interval
        /// </summary>
        public TimeSpan TtlHeartbeatInterval { get; }

        /// <summary>
        /// The key prefix used for all Redis keys
        /// </summary>
        public string KeyPrefix { get; }

        /// <summary>
        /// Creates a copy with a different service name
        /// </summary>
        public RedisDiscoverySettings WithServiceName(string serviceName)
            => Copy(serviceName: serviceName);

        /// <summary>
        /// Creates a copy with a different hostname
        /// </summary>
        public RedisDiscoverySettings WithHostName(string hostName)
            => Copy(hostName: hostName);

        /// <summary>
        /// Creates a copy with a different port
        /// </summary>
        public RedisDiscoverySettings WithPort(int port)
            => Copy(port: port);

        /// <summary>
        /// Creates a copy with a different connection string
        /// </summary>
        public RedisDiscoverySettings WithConnectionString(string connectionString)
            => Copy(connectionString: connectionString);

        /// <summary>
        /// Creates a copy with a different TTL
        /// </summary>
        public RedisDiscoverySettings WithTtl(TimeSpan ttl)
            => Copy(ttl: ttl);

        /// <summary>
        /// Creates a copy with a different TTL heartbeat interval
        /// </summary>
        public RedisDiscoverySettings WithTtlHeartbeatInterval(TimeSpan ttlHeartbeatInterval)
            => Copy(ttlHeartbeatInterval: ttlHeartbeatInterval);

        /// <summary>
        /// Creates a copy with a different key prefix
        /// </summary>
        public RedisDiscoverySettings WithKeyPrefix(string keyPrefix)
            => Copy(keyPrefix: keyPrefix);

        private RedisDiscoverySettings Copy(
            string? serviceName = null,
            string? hostName = null,
            int? port = null,
            string? connectionString = null,
            TimeSpan? ttl = null,
            TimeSpan? ttlHeartbeatInterval = null,
            string? keyPrefix = null)
            => new(
                serviceName: serviceName ?? ServiceName,
                hostName: hostName ?? HostName,
                port: port ?? Port,
                connectionString: connectionString ?? ConnectionString,
                ttl: ttl ?? Ttl,
                ttlHeartbeatInterval: ttlHeartbeatInterval ?? TtlHeartbeatInterval,
                keyPrefix: keyPrefix ?? KeyPrefix);

        /// <inheritdoc/>
        public override string ToString()
            => $"[RedisDiscoverySettings](" +
               $"{nameof(ServiceName)}:{ServiceName}, " +
               $"{nameof(HostName)}:{HostName}, " +
               $"{nameof(Port)}:{Port}, " +
               $"{nameof(ConnectionString)}:{ConnectionString}, " +
               $"{nameof(Ttl)}:{Ttl}, " +
               $"{nameof(TtlHeartbeatInterval)}:{TtlHeartbeatInterval}, " +
               $"{nameof(KeyPrefix)}:{KeyPrefix})";
    }
}
