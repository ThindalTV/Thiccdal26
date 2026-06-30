using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Data.Tests;

public sealed class SetupStateServiceTests : IAsyncDisposable
{
    private readonly InMemoryApplicationDbContextFactory _contextFactory;
    private readonly SetupStateService _service;

    public SetupStateServiceTests()
    {
        _contextFactory = new InMemoryApplicationDbContextFactory();
        _service = new SetupStateService(
            _contextFactory,
            NullLogger<SetupStateService>.Instance);
    }

    public ValueTask DisposeAsync() => _contextFactory.DisposeAsync();

    [Fact]
    public async Task WhenNoConfigurationExists_ThenStateIsNotStarted()
    {
        var state = await _service.GetSetupState();

        Assert.Equal(SetupState.NotStarted, state);
    }

    [Fact]
    public async Task WhenCurrentStepIsSet_ThenStateIsInProgress()
    {
        await _service.SetCurrentStepIndex(2);

        var state = await _service.GetSetupState();

        Assert.Equal(SetupState.InProgress, state);
    }

    [Fact]
    public async Task WhenSetupIsMarkedComplete_ThenStateIsComplete()
    {
        await _service.MarkSetupComplete();

        var state = await _service.GetSetupState();

        Assert.Equal(SetupState.Complete, state);
    }

    [Fact]
    public async Task WhenSetupIsMarkedComplete_ThenIsSetupCompleteReturnsTrue()
    {
        await _service.MarkSetupComplete();

        var isComplete = await _service.IsSetupComplete();

        Assert.True(isComplete);
    }

    [Fact]
    public async Task WhenNoConfigurationExists_ThenIsSetupCompleteReturnsFalse()
    {
        var isComplete = await _service.IsSetupComplete();

        Assert.False(isComplete);
    }

    [Fact]
    public async Task WhenCurrentStepIsSet_ThenGetCurrentStepIndexReturnsCorrectValue()
    {
        await _service.SetCurrentStepIndex(3);

        var stepIndex = await _service.GetCurrentStepIndex();

        Assert.Equal(3, stepIndex);
    }

    [Fact]
    public async Task WhenNoStepIsSet_ThenGetCurrentStepIndexReturnsZero()
    {
        var stepIndex = await _service.GetCurrentStepIndex();

        Assert.Equal(0, stepIndex);
    }

    [Fact]
    public async Task WhenStepIsUpdated_ThenNewValueIsPersisted()
    {
        await _service.SetCurrentStepIndex(1);
        await _service.SetCurrentStepIndex(4);

        var stepIndex = await _service.GetCurrentStepIndex();

        Assert.Equal(4, stepIndex);
    }

    [Fact]
    public async Task WhenSetupCompleteAndStepSet_ThenStateIsStillComplete()
    {
        await _service.SetCurrentStepIndex(2);
        await _service.MarkSetupComplete();

        var state = await _service.GetSetupState();

        Assert.Equal(SetupState.Complete, state);
    }
}
