using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class WizardStepCatalogTests
{
    [Fact]
    public void GetSteps_ReturnsCorrectNumberOfSteps()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(7, steps.Count);
    }

    [Fact]
    public void GetSteps_FirstStepIsWelcome()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(WizardStep.Welcome, steps[0].Step);
        Assert.Equal("Welcome", steps[0].Name);
    }

    [Fact]
    public void GetSteps_LastStepIsSummary()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(WizardStep.Summary, steps[^1].Step);
        Assert.Equal("Summary", steps[^1].Name);
    }

    [Fact]
    public void GetSteps_StepsAreInCorrectOrder()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(WizardStep.Welcome, steps[0].Step);
        Assert.Equal(WizardStep.Database, steps[1].Step);
        Assert.Equal(WizardStep.Streaming, steps[2].Step);
        Assert.Equal(WizardStep.Platforms, steps[3].Step);
        Assert.Equal(WizardStep.AiSetup, steps[4].Step);
        Assert.Equal(WizardStep.BotConfig, steps[5].Step);
        Assert.Equal(WizardStep.Summary, steps[6].Step);
    }

    [Theory]
    [InlineData(0, false)] // Welcome - required
    [InlineData(1, false)] // Database - required
    [InlineData(2, false)] // Streaming - required
    [InlineData(3, true)]  // Platforms - optional
    [InlineData(4, true)]  // AI Setup - optional
    [InlineData(5, false)] // Bot Config - required
    [InlineData(6, false)] // Summary - required
    public void GetSteps_OptionalFlagIsCorrect(int index, bool expectedOptional)
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(expectedOptional, steps[index].IsOptional);
    }

    [Fact]
    public void GetSteps_AllStepsHaveNames()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.All(steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Name)));
    }

    [Fact]
    public void GetSteps_AllStepsHaveDescriptions()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.All(steps, step => Assert.False(string.IsNullOrWhiteSpace(step.Description)));
    }

    [Fact]
    public void WizardStepModel_WithComplete_ReturnsNewRecordWithIsCompleteTrue()
    {
        var step = new WizardStepModel(WizardStep.Welcome, "Welcome", "Description", IsOptional: false);

        var completedStep = step with { IsComplete = true };

        Assert.True(completedStep.IsComplete);
        Assert.False(step.IsComplete);
    }
}
