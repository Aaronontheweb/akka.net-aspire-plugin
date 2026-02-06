using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Xunit;

namespace Aaron.Akka.Aspire.Hosting.Tests;

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
    public async Task Service_should_start_with_health_checks_responding()
    {
        var endpoint = _app!.GetEndpoint("service", "http");
        using var client = new HttpClient { BaseAddress = endpoint };

        // Retry until the health check endpoint responds (service may need time to bind its HTTP port)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        HttpResponseMessage? response = null;
        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                response = await client.GetAsync("/healthz", cts.Token);
                break;
            }
            catch (HttpRequestException) when (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(500, cts.Token);
            }
        }

        response.Should().NotBeNull();

        // The health check endpoint should respond - it may return 503 (ServiceUnavailable) because
        // the akka-cluster-membership check reports unhealthy until the cluster fully forms.
        // Both 200 and 503 confirm the health check infrastructure is working correctly.
        response!.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
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
