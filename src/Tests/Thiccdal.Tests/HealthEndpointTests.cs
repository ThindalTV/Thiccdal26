using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Thiccdal.Tests;

public sealed class HealthEndpointTests : IClassFixture<ThiccdalApplicationFactory>
{
    private readonly ThiccdalApplicationFactory _applicationFactory;

    public HealthEndpointTests(ThiccdalApplicationFactory applicationFactory)
    {
        _applicationFactory = applicationFactory;
    }

    [Fact]
    public async Task WhenHealthEndpointCalled_ThenLivenessReturnsHealthy()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenReadyEndpointCalled_ThenReadinessReturnsHealthy()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WhenReadinessCheckFails_ThenHealthStaysHealthyWhileReadyReturnsUnavailable()
    {
        using HttpClient client = CreateClient(
            builder =>
            {
                builder.ConfigureServices(
                    services =>
                    {
                        services.AddHealthChecks()
                            .AddCheck(
                                "forced-ready-failure",
                                static () => HealthCheckResult.Unhealthy("forced failure"),
                                tags: ["ready"]);
                    });
            });

        HttpResponseMessage healthResponse = await client.GetAsync("/health");
        HttpResponseMessage readyResponse = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
    }

    private HttpClient CreateClient(Action<IWebHostBuilder>? configureWebHost = null)
    {
        WebApplicationFactory<Program> factory = _applicationFactory;
        if (configureWebHost is not null)
        {
            factory = factory.WithWebHostBuilder(configureWebHost);
        }

        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }
}
