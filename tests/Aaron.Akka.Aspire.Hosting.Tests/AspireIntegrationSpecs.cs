using System.Diagnostics.CodeAnalysis;
using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Xunit;

namespace Aaron.Akka.Aspire.Hosting.Tests;

[SuppressMessage("Slopwatch", "SW003", Justification = "Polling loop intentionally retries on transient HTTP failures")]
[SuppressMessage("Slopwatch", "SW004", Justification = "Polling interval for cluster formation - not a timing-dependent test")]
public sealed class AspireIntegrationSpecs : IAsyncLifetime
{
    private DistributedApplication? _app;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Aaron_Akka_Aspire_Sample_AppHost>();

        _app = await builder.BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await _app.StartAsync(cts.Token);

        // Wait for the service resource to start running
        await _app.ResourceNotifications
            .WaitForResourceAsync("service", KnownResourceStates.Running, cts.Token);
    }

    [Fact]
    public async Task Service_should_form_cluster_with_healthy_status()
    {
        var endpoint = _app!.GetEndpoint("service", "http");
        using var client = new HttpClient { BaseAddress = endpoint };

        // Poll /healthz until it returns 200, proving the cluster has formed
        // (the akka-cluster-membership health check only returns Healthy when MemberStatus == Up)
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        HttpResponseMessage? response = null;
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                response = await client.GetAsync("/healthz", cts.Token);
                if (response.StatusCode == HttpStatusCode.OK)
                    break;
            }
            catch (HttpRequestException) when (!cts.Token.IsCancellationRequested)
            {
                // Service may not be ready yet
            }

            await Task.Delay(1000, cts.Token);
        }

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(HttpStatusCode.OK,
            "health check should return 200 once the cluster has formed and member status is Up");
    }

    [Fact]
    public async Task Service_should_respond_to_root_endpoint()
    {
        var endpoint = _app!.GetEndpoint("service", "http");
        using var client = new HttpClient { BaseAddress = endpoint };
        var response = await client.GetStringAsync("/");
        response.Should().Contain("Hello from Akka.NET Aspire");
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
