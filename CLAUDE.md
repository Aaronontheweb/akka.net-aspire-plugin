# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
dotnet tool restore                          # restore local tools (slopwatch, incrementalist)
pwsh ./build.ps1                             # inject version from RELEASE_NOTES.md into Directory.Build.props
dotnet build -c Release                      # build everything
dotnet test -c Release                       # run all tests
dotnet test <project> -c Release --filter "FullyQualifiedName~ClassName"  # run a single test class
dotnet slopwatch                             # code style linter - must pass in CI
dotnet pack -c Release -o ./bin/nuget        # create NuGet packages
```

**Incrementalist** runs tests selectively based on git changes against the `dev` branch:
```bash
dotnet incrementalist run --config .incrementalist/incrementalist.json -- test -c Release   # all tests (Linux CI)
dotnet incrementalist run --config .incrementalist/testsOnly.json -- test -c Release         # skip Docker tests (Windows CI)
```

## Build System

- `build.ps1` reads the version from the first line of `RELEASE_NOTES.md` and injects `VersionPrefix` + `PackageReleaseNotes` into `Directory.Build.props`. **Do NOT manually edit version fields in `Directory.Build.props`.**
- `TreatWarningsAsErrors=true` across all projects.
- Central package management via `Directory.Packages.props`. Akka.NET packages use `$(AkkaManagementVersion)`.
- Releases are triggered by pushing a git tag. The `publish_nuget.yml` workflow packs with the tag as the version and pushes to nuget.org.

## Architecture

Three NuGet packages that work together:

### `Aaron.Akka.Aspire.Hosting` (net10.0) - AppHost side
Used in the Aspire AppHost project. Provides `AddAkka()`, `WithClustering()`, and `WithReference()` extension methods. Injects environment variables (`Akka__Cluster__*`) and configures `akka-remote` (TCP) and `akka-management` (HTTP) endpoints. Detects replica count from Aspire annotations.

### `Aaron.Akka.Aspire` (net10.0) - Service side
Used in application services. `WithAspireClusterBootstrap()` reads `IConfiguration` section `Akka:Cluster` (populated from the environment variables above), then configures Akka.Remote, Akka.Cluster, Akka.Management, and Cluster Bootstrap. Also injects discovery plugin HOCON (`public-hostname`/`public-port`) so each replica registers uniquely. Includes a cluster membership health check.

### `Aaron.Akka.Discovery.Redis` (netstandard2.0;net9.0;net10.0) - Discovery plugin
Redis-based service discovery for Akka.NET. Each node registers itself in Redis with a heartbeat; other nodes query Redis to find cluster members. Has an embedded `reference.conf`.

### Configuration flow
```
Aspire AppHost (AddAkka + WithClustering + WithReference)
  → environment variables + connection strings injected into service containers
    → AkkaAspireClusterSettings reads IConfiguration("Akka:Cluster")
      → WithAspireClusterBootstrap configures the full Akka cluster stack
```

## Testing

- **Unit tests** (`Aaron.Akka.Aspire.Tests`, `Aaron.Akka.Discovery.Redis.Tests`): Use `Akka.Hosting.TestKit`. Set `autoStartBootstrap: false` to prevent bootstrap from killing the actor system in tests. Use `global::Akka.Hosting.TestKit.TestKit` to avoid namespace collision with `Aaron.Akka.Hosting`.
- **Aspire integration tests** (`Aaron.Akka.Aspire.Hosting.Tests/AspireIntegrationSpecs.cs`): Require Docker (Redis container). Use `DistributedApplicationTestingBuilder`. Skipped on Windows CI via incrementalist config.
- **Hosting unit tests** (`Aaron.Akka.Aspire.Hosting.Tests/AkkaServiceExtensionsSpecs.cs`): Test endpoint and environment variable configuration without Docker.

## Key Technical Details

- `Akka.Management.Cluster.Bootstrap` NuGet package is **deprecated**. Cluster bootstrap is folded into `Akka.Management` 1.5.59+. The assembly name is `Akka.Management`.
- `Akka.Management` does NOT provide health checks. This project implements its own `WithClusterMembershipCheck()`.
- Akka Management `hostName` must match the discovery target hostname (e.g. "localhost"), not "0.0.0.0". Use `bindHostname: "0.0.0.0"` for the listening interface. Otherwise `SelfAwareJoinDecider` can't match the self contact point to discovered targets.
- With `isProxied: true` + `WithReplicas()`, each replica gets a unique target port. `isProxied: false` is incompatible with replicas.
- Slopwatch suppressions go in `.slopwatch/config.json`, not `[SuppressMessage]` attributes (which trigger SW002).

## Aspire MCP Tools

When running with `aspire run`, use the Aspire MCP tools to inspect resource state:
- `list resources` / `execute resource command` for resource status
- `list structured logs` / `list console logs` / `list traces` for debugging
- `list integrations` / `get integration docs` before adding new Aspire resources
