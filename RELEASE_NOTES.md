#### 0.2.0 May 28 2026 ####

**Security:**
- Updated OpenTelemetry packages to 1.15.x — patches 4 known vulnerabilities (GHSA-g94r-2vxg-569j, GHSA-4625-4j76-fww9, GHSA-mr8r-92fq-pj8p, GHSA-q834-8qmm-v933)

**Dependencies:**
- .NET Aspire updated to 13.3.5
- Akka.NET updated to 1.5.60
- Akka.Hosting updated to 1.5.59 (with cluster bootstrap folded into core)
- Akka.Cluster.Hosting updated to 1.5.59

#### 0.1.0 February 6 2026 ####

Initial release of Akka.NET Aspire hosting plugin packages:

**Packages:**
* `Aaron.Akka.Aspire.Hosting` - AppHost integration for configuring Akka.NET clusters in .NET Aspire
* `Aaron.Akka.Aspire` - Client/service-side Akka.NET Aspire cluster bootstrap
* `Aaron.Akka.Discovery.Redis` - Redis-based service discovery plugin for Akka.NET

**Features:**
* Health checks for cluster membership and actor system liveness via ASP.NET health check infrastructure [#3](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/3)
* Flexible discovery provider configuration via `configureDiscovery` callback parameter in `WithAspireClusterBootstrap` [#10](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/10)
* Azure Table Storage discovery sample project demonstrating multi-provider portability [#10](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/10)
* Working Redis-based sample application with 3 replicas demonstrating cluster formation
* Comprehensive documentation covering architecture, configuration, and deployment scenarios [#9](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/9)
* Full Akka.Hosting extension method integration (no raw HOCON required)

**Bug Fixes:**
* Fixed cluster bootstrap: replicas can now properly discover each other and form clusters [#7](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/7)
  - Resolved SelfAwareJoinDecider hostname mismatch between management config and discovery targets
  - Each replica now registers unique `(hostname, port)` tuple in Redis discovery
* Added OpenTelemetry integration for Aspire dashboard visibility

**Technical Details:**
* Akka.Management 1.5.59+ (cluster bootstrap folded into core `Akka.Management` package)
* Supports .NET 10.0 (Hosting + Client packages), netstandard2.0/net9.0/net10.0 (Redis discovery)
* Integration tests verify cluster formation with Docker/Redis (~15s for 3 replicas)
* Code quality checks via slopwatch [#5](https://github.com/Aaronontheweb/akka.net-aspire-plugin/pull/5)
