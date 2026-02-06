using Aaron.Akka.Aspire;
using Aaron.Akka.Discovery.Redis;
using Akka.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry - ships logs, traces, and metrics to the Aspire dashboard
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    })
    .UseOtlpExporter();

builder.Services.AddAkka("SampleSystem", (akkaBuilder, sp) =>
{
    akkaBuilder.ConfigureLoggers(setup =>
    {
        setup.ClearLoggers();
        setup.AddLoggerFactory();
    });

    var config = sp.GetRequiredService<IConfiguration>();
    var redisConn = config.GetConnectionString("akka-discovery");
    var serviceName = config["Akka:Cluster:ServiceName"];

    if (!string.IsNullOrEmpty(redisConn))
    {
        akkaBuilder.WithRedisDiscovery(redisConn, serviceName);
    }

    akkaBuilder.WithAspireClusterBootstrap(sp, cluster =>
    {
        cluster.Roles = ["sample"];
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = c => c.Tags.Contains("liveness") });
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("readiness") });
app.MapGet("/", () => "Hello from Akka.NET Aspire Sample!");

app.Run();
