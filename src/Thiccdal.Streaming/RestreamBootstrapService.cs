using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

public sealed class RestreamBootstrapService : IHostedService
{
    private readonly IRestreamRuntimeService _restreamRuntimeService;
    private readonly ILogger<RestreamBootstrapService> _logger;

    public RestreamBootstrapService(
        IRestreamRuntimeService restreamRuntimeService,
        ILogger<RestreamBootstrapService> logger)
    {
        ArgumentNullException.ThrowIfNull(restreamRuntimeService);
        ArgumentNullException.ThrowIfNull(logger);

        _restreamRuntimeService = restreamRuntimeService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        RestreamControlState state = await _restreamRuntimeService.GetState(cancellationToken);
        if (!state.StartWithHost)
        {
            return;
        }

        RestreamControlState startedState = await _restreamRuntimeService.Start(cancellationToken);
        _logger.LogInformation(
            "Restream bootstrap evaluated start-with-host and returned canStart={CanStart}, ingestRunning={IsIngestRunning}, fanoutRunning={IsFanoutRunning}.",
            startedState.CanStart,
            startedState.IsIngestRunning,
            startedState.IsFanoutRunning);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
