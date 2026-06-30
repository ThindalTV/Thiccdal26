using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Data;

/// <summary>
/// Manages installation wizard state using the database.
/// </summary>
public sealed class SetupStateService : ISetupStateService
{
    private const string SetupCompleteKey = "SetupComplete";
    private const string CurrentStepKey = "SetupCurrentStep";

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<SetupStateService> _logger;

    public SetupStateService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<SetupStateService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<SetupState> GetSetupState(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var completeConfig = await context.AppConfigurations
                .FirstOrDefaultAsync(c => c.Key == SetupCompleteKey, cancellationToken);

            if (completeConfig is not null && bool.TryParse(completeConfig.Value, out var isComplete) && isComplete)
                return SetupState.Complete;

            var stepConfig = await context.AppConfigurations
                .FirstOrDefaultAsync(c => c.Key == CurrentStepKey, cancellationToken);

            return stepConfig is not null ? SetupState.InProgress : SetupState.NotStarted;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine setup state from database, assuming not started");
            return SetupState.NotStarted;
        }
    }

    public async Task<int> GetCurrentStepIndex(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

            var config = await context.AppConfigurations
                .FirstOrDefaultAsync(c => c.Key == CurrentStepKey, cancellationToken);

            return config is not null && int.TryParse(config.Value, out var step) ? step : 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task SetCurrentStepIndex(int stepIndex, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = await context.AppConfigurations
            .FirstOrDefaultAsync(c => c.Key == CurrentStepKey, cancellationToken);

        if (config is null)
        {
            config = new AppConfiguration { Key = CurrentStepKey };
            context.AppConfigurations.Add(config);
        }

        config.Value = stepIndex.ToString();
        config.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSetupComplete(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = await context.AppConfigurations
            .FirstOrDefaultAsync(c => c.Key == SetupCompleteKey, cancellationToken);

        if (config is null)
        {
            config = new AppConfiguration { Key = SetupCompleteKey };
            context.AppConfigurations.Add(config);
        }

        config.Value = "true";
        config.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Installation wizard marked as complete");
    }

    public async Task<bool> IsSetupComplete(CancellationToken cancellationToken = default)
    {
        var state = await GetSetupState(cancellationToken);
        return state == SetupState.Complete;
    }
}
