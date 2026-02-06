namespace Aaron.Akka.Aspire.Hosting;

/// <summary>
/// Internal implementation of <see cref="IClusteringProvider"/> that auto-detects the provider type
/// from the resource type name and configures connection strings and environment variables.
/// </summary>
internal sealed class ClusteringProvider : IClusteringProvider
{
    private readonly IResourceBuilder<IResourceWithConnectionString> _resource;
    private readonly string _providerType;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClusteringProvider"/> class.
    /// </summary>
    /// <param name="resource">The resource builder that provides the connection string.</param>
    public ClusteringProvider(IResourceBuilder<IResourceWithConnectionString> resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));

        // Auto-detect provider type by stripping "Resource" suffix from the resource type name
        // e.g., "RedisResource" -> "Redis", "PostgresServerResource" -> "PostgresServer"
        var resourceTypeName = resource.Resource.GetType().Name;
        _providerType = resourceTypeName.EndsWith("Resource", StringComparison.Ordinal)
            ? resourceTypeName[..^"Resource".Length]
            : resourceTypeName;
    }

    /// <summary>
    /// Configures the specified resource builder with clustering settings.
    /// </summary>
    /// <typeparam name="T">The resource type that supports environment variables.</typeparam>
    /// <param name="builder">The resource builder to configure.</param>
    public void ConfigureResource<T>(IResourceBuilder<T> builder) where T : IResourceWithEnvironment
    {
        // Inject the connection string reference
        builder.WithReference(_resource);

        // Set the provider type environment variable
        builder.WithEnvironment("Akka__Cluster__Clustering__ProviderType", _providerType);
    }
}
