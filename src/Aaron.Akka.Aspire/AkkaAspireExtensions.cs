using Akka.Cluster.Hosting;
using Akka.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aaron.Akka.Aspire;

/// <summary>
/// Extension methods for configuring Akka.NET with Aspire cluster bootstrap.
/// </summary>
public static class AkkaAspireExtensions
{
    /// <summary>
    /// Configures Akka.NET with Aspire cluster bootstrap settings.
    /// Reads configuration from the 'Akka:Cluster' section and sets up Akka.Remote,
    /// Akka.Cluster, Akka.Management, and Cluster Bootstrap when enabled.
    /// </summary>
    /// <param name="builder">The Akka configuration builder.</param>
    /// <param name="sp">The service provider for accessing IConfiguration.</param>
    /// <param name="clusterConfigure">Optional callback to customize cluster options.</param>
    /// <returns>The Akka configuration builder for method chaining.</returns>
    public static AkkaConfigurationBuilder WithAspireClusterBootstrap(
        this AkkaConfigurationBuilder builder,
        IServiceProvider sp,
        Action<ClusterOptions>? clusterConfigure = null)
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var settings = new AkkaAspireClusterSettings();
        configuration.GetSection("Akka:Cluster").Bind(settings);

        // If clustering is not enabled, return immediately without configuration
        if (!settings.Enabled)
        {
            return builder;
        }

        // Configure Akka.Remote
        builder.AddHocon($@"
akka.remote.dot-netty.tcp {{
    hostname = ""0.0.0.0""
    port = {settings.RemotePort}
    public-hostname = ""{settings.PublicHostName}""
    public-port = {settings.RemotePort}
}}", HoconAddMode.Prepend);

        // Configure Akka.Cluster with empty seed nodes (bootstrap will handle discovery)
        var clusterOptions = new ClusterOptions
        {
            SeedNodes = Array.Empty<string>()
        };
        clusterConfigure?.Invoke(clusterOptions);
        builder.WithClustering(clusterOptions);

        // Configure Akka.Management HTTP endpoint
        builder.AddHocon($@"
akka.management {{
    http {{
        hostname = ""0.0.0.0""
        port = {settings.ManagementPort}
        bind-hostname = ""0.0.0.0""
        bind-port = {settings.ManagementPort}
    }}
}}", HoconAddMode.Prepend);

        // Configure Cluster Bootstrap
        builder.AddHocon($@"
akka.management.cluster.bootstrap {{
    contact-point-discovery {{
        required-contact-point-nr = {settings.RequiredContactPointsNr}
        service-name = ""{settings.ServiceName}""
        stable-margin = 5s
    }}
    contact-point {{
        filter-on-fallback-port = {settings.FilterOnFallbackPort.ToString().ToLowerInvariant()}
    }}
}}", HoconAddMode.Prepend);

        // Determine discovery method from provider type
        var discoveryMethod = DetermineDiscoveryMethod(settings.Clustering?.ProviderType);
        builder.AddHocon($@"
akka.discovery.method = ""{discoveryMethod}""
", HoconAddMode.Prepend);

        // Configure health checks
        builder.AddHocon(@"
akka.management.http.health-checks {
    readiness-checks {
        cluster-membership = ""Akka.Management.Cluster.ClusterMembershipCheck, Akka.Management.Cluster.Bootstrap""
    }
    liveness-checks {
        cluster-membership = ""Akka.Management.Cluster.ClusterMembershipCheck, Akka.Management.Cluster.Bootstrap""
    }
}
", HoconAddMode.Prepend);

        return builder;
    }

    private static string DetermineDiscoveryMethod(string? providerType)
    {
        if (string.IsNullOrEmpty(providerType))
        {
            return "config";
        }

        return providerType.ToLowerInvariant() switch
        {
            "redis" => "redis",
            "azuretablestorage" => "azure",
            "kubernetes" => "kubernetes-api",
            "config" => "config",
            _ => "config"
        };
    }
}
