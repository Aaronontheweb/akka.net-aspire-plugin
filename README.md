# Akka.NET Aspire Plugin

Automated Akka.NET cluster formation for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire). Configure your cluster topology in the AppHost, and each service replica will automatically discover peers, form a cluster, and report health status.

## Packages

| Package | Target | Description |
|---------|--------|-------------|
| `Aaron.Akka.Aspire.Hosting` | net10.0 | AppHost-side: `AddAkka()`, `WithClustering()`, `WithReference()` |
| `Aaron.Akka.Aspire` | net10.0 | Service-side: `WithAspireClusterBootstrap()` reads Aspire-injected config |
| `Aaron.Akka.Discovery.Redis` | netstandard2.0; net9.0; net10.0 | Redis-based service discovery plugin |

## Usage

### AppHost

```csharp
using Aaron.Akka.Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("akka-discovery");

var akka = builder.AddAkka("my-cluster")
    .WithClustering(redis);

builder.AddProject<Projects.MyService>("service")
    .WithHttpEndpoint(name: "http")
    .WithReplicas(3)
    .WithReference(akka);

builder.Build().Run();
```

### Service

```csharp
using Aaron.Akka.Aspire;
using Aaron.Akka.Discovery.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAkka("MySystem", (akkaBuilder, sp) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var redisConn = config.GetConnectionString("akka-discovery");
    var serviceName = config["Akka:Cluster:ServiceName"];

    if (!string.IsNullOrEmpty(redisConn))
        akkaBuilder.WithRedisDiscovery(redisConn, serviceName);

    akkaBuilder.WithAspireClusterBootstrap(sp);
});

builder.Services.AddHealthChecks();
var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapGet("/", () => "Hello from Akka.NET!");
app.Run();
```

`WithAspireClusterBootstrap` configures Akka.Remote, Akka.Cluster, Akka.Management, Cluster Bootstrap, and health checks from the environment variables that the hosting package injects. No manual HOCON needed.

## How It Works

The hosting package (`WithReference(akka)`) injects environment variables into each service replica:

- `Akka__Cluster__Enabled` - enables clustering
- `Akka__Cluster__RemotePort` / `Akka__Cluster__ManagementPort` - unique ports per replica
- `Akka__Cluster__PublicHostName` / `Akka__Cluster__ServiceName` - discovery identity
- `Akka__Cluster__RequiredContactPointsNr` - derived from replica count
- Connection string for the discovery backend (e.g. Redis)

The service-side bootstrap reads these via `IConfiguration`, configures the full Akka.NET cluster stack, and uses the discovery plugin to find other replicas. Cluster Bootstrap's `SelfAwareJoinDecider` handles initial seed node election.

## Supported Discovery Providers

- **Redis** (`Aaron.Akka.Discovery.Redis`) - each node registers in Redis with a heartbeat
- **Azure Table Storage** - via `Akka.Discovery.Azure`
- **Kubernetes** - via `Akka.Discovery.KubernetesApi`
- **Config** - static seed nodes (default fallback)

## Learn More

- [Akka.NET Clustering](https://getakka.net/articles/clustering/cluster-overview.html) - how Akka.NET clusters work, membership lifecycle, and seed node discovery
- [Akka.Management](https://github.com/akkadotnet/Akka.Management) - HTTP management endpoint and Cluster Bootstrap for automated cluster formation
- [Akka.Hosting](https://github.com/akkadotnet/Akka.Hosting) - `IServiceCollection` integration for configuring Akka.NET without raw HOCON
- [.NET Aspire](https://aspire.dev/get-started/what-is-aspire/) - orchestration, service discovery, and telemetry for distributed .NET apps

## Building

```bash
dotnet tool restore
dotnet build -c Release
dotnet test -c Release
dotnet pack -c Release -o ./bin/nuget
```

Integration tests require Docker (they spin up a Redis container via Aspire).

## License

Apache-2.0
