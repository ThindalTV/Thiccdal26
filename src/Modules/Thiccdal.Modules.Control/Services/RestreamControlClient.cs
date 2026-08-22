using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Modules.Control.Services;

public sealed class RestreamControlClient : IRestreamControlClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;

    public RestreamControlClient(IHttpClientFactory httpClientFactory, NavigationManager navigationManager)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(navigationManager);

        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
    }

    public Task<RestreamControlState> GetState(CancellationToken cancellationToken = default)
    {
        return SendGet("/api/restream", cancellationToken);
    }

    public Task<RestreamControlState> UpdateConfiguration(
        RestreamConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SendPut("/api/restream/configuration", request, cancellationToken);
    }

    public Task<RestreamControlState> PushConfiguration(CancellationToken cancellationToken = default)
    {
        return SendPost("/api/restream/push", cancellationToken);
    }

    public Task<RestreamControlState> UpdateDestination(RestreamDestinationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SendPut($"/api/restream/destinations/{Uri.EscapeDataString(request.PlatformName)}", request, cancellationToken);
    }

    public Task<RestreamControlState> Start(CancellationToken cancellationToken = default)
    {
        return SendPost("/api/restream/start", cancellationToken);
    }

    public Task<RestreamControlState> Stop(CancellationToken cancellationToken = default)
    {
        return SendPost("/api/restream/stop", cancellationToken);
    }

    private async Task<RestreamControlState> SendGet(string url, CancellationToken cancellationToken)
    {
        HttpClient client = CreateClient();
        RestreamControlState? state = await client.GetFromJsonAsync<RestreamControlState>(url, cancellationToken);
        return state ?? throw new InvalidOperationException("The restream API returned no state payload.");
    }

    private async Task<RestreamControlState> SendPost(string url, CancellationToken cancellationToken)
    {
        HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.PostAsync(url, content: null, cancellationToken);
        response.EnsureSuccessStatusCode();

        RestreamControlState? state = await response.Content.ReadFromJsonAsync<RestreamControlState>(cancellationToken);
        return state ?? throw new InvalidOperationException("The restream API returned no state payload.");
    }

    private async Task<RestreamControlState> SendPut(
        string url,
        object request,
        CancellationToken cancellationToken)
    {
        HttpClient client = CreateClient();
        using HttpResponseMessage response = await client.PutAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        RestreamControlState? state = await response.Content.ReadFromJsonAsync<RestreamControlState>(cancellationToken);
        return state ?? throw new InvalidOperationException("The restream API returned no state payload.");
    }

    private HttpClient CreateClient()
    {
        HttpClient client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_navigationManager.BaseUri);
        return client;
    }
}
