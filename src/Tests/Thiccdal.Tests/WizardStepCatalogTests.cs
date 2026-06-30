using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class WizardStepCatalogTests
{
    [Fact]
    public void GetSteps_ReturnsCorrectNumberOfSteps()
    {
        var steps = WizardStepCatalog.GetSteps();

        Assert.Equal(6, steps.Count);
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
        Assert.Equal(WizardStep.Platforms, steps[2].Step);
        Assert.Equal(WizardStep.AiSetup, steps[3].Step);
        Assert.Equal(WizardStep.BotConfig, steps[4].Step);
        Assert.Equal(WizardStep.Summary, steps[5].Step);
    }

    [Theory]
    [InlineData(0, false)] // Welcome - required
    [InlineData(1, false)] // Database - required
    [InlineData(2, true)]  // Platforms - optional
    [InlineData(3, true)]  // AI Setup - optional
    [InlineData(4, false)] // Bot Config - required
    [InlineData(5, false)] // Summary - required
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
