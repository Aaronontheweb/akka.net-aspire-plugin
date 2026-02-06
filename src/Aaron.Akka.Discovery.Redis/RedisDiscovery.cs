#nullable enable
using Akka.Actor;
using Akka.Configuration;

namespace Aaron.Akka.Discovery.Redis
{
    /// <summary>
    /// Extension for Redis-based service discovery
    /// </summary>
    public class RedisDiscovery : IExtension
    {
        /// <summary>
        /// Gets the default configuration for Redis discovery
        /// </summary>
        public static Config DefaultConfiguration()
            => ConfigurationFactory.FromResource<RedisDiscovery>("Aaron.Akka.Discovery.Redis.reference.conf");

        /// <summary>
        /// Gets the RedisDiscovery extension for the actor system
        /// </summary>
        public static RedisDiscovery Get(ActorSystem system)
            => system.WithExtension<RedisDiscovery, RedisDiscoveryProvider>();

        /// <summary>
        /// The discovery settings
        /// </summary>
        public readonly RedisDiscoverySettings Settings;

        /// <summary>
        /// Creates a new RedisDiscovery extension
        /// </summary>
        public RedisDiscovery(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfiguration());
            Settings = RedisDiscoverySettings.Create(system);
        }
    }

    /// <summary>
    /// Extension provider for RedisDiscovery
    /// </summary>
    public class RedisDiscoveryProvider : ExtensionIdProvider<RedisDiscovery>
    {
        /// <inheritdoc/>
        public override RedisDiscovery CreateExtension(ExtendedActorSystem system)
            => new RedisDiscovery(system);
    }
}
