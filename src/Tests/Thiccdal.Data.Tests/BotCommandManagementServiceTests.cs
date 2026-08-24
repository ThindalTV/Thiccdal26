using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Tests;

public sealed class BotCommandManagementServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenListingCommandsForFirstTime_ThenStarterCommandsAreSeeded()
    {
        BotCommandManagementService service = new(DbContextFactory);

        IReadOnlyList<BotCommandDefinition> commands = await service.List(CancellationToken.None);

        Assert.NotEmpty(commands);
        Assert.Contains(commands, command => command.Trigger == "!shoutout");
        Assert.Contains(commands, command => command.Trigger == "!poll" && !command.IsEnabled);
    }

    [Fact]
    public async Task WhenCommandDeclaresEffects_ThenTheyArePersisted()
    {
        BotCommandManagementService service = new(DbContextFactory);

        BotCommandDefinition createdCommand = await service.Create(
            new BotCommandDefinitionInput(
                "brb",
                "Be right back",
                null,
                true,
                SendInChat: false,
                ShowOnLowerThird: true,
                LowerThirdTitle: "  BRB  ",
                LowerThirdText: "Back in five"),
            CancellationToken.None);

        IReadOnlyList<BotCommandDefinition> commands = await service.List(CancellationToken.None);
        BotCommandDefinition savedCommand = commands.Single(command => command.Id == createdCommand.Id);

        Assert.False(savedCommand.SendInChat);
        Assert.True(savedCommand.ShowOnLowerThird);
        Assert.Equal("BRB", savedCommand.LowerThirdTitle);
        Assert.Equal("Back in five", savedCommand.LowerThirdText);
    }

    [Fact]
    public async Task WhenSeedCommandsAreCreated_ThenTheyReplyInChat()
    {
        BotCommandManagementService service = new(DbContextFactory);

        IReadOnlyList<BotCommandDefinition> commands = await service.List(CancellationToken.None);

        Assert.All(commands, command => Assert.True(command.SendInChat));
    }

    [Fact]
    public async Task WhenCreatingCommandWithoutBang_ThenTriggerIsNormalized()
    {
        BotCommandManagementService service = new(DbContextFactory);

        BotCommandDefinition createdCommand = await service.Create(
            new BotCommandDefinitionInput("Raid", "Thanks for the raid, {user}!", null, true),
            CancellationToken.None);

        Assert.Equal("!raid", createdCommand.Trigger);
        Assert.Equal("Thanks for the raid, {user}!", createdCommand.ResponseTemplate);
        Assert.Equal(0, createdCommand.UseCount);
    }

    [Fact]
    public async Task WhenUpdatingExistingCommand_ThenValuesArePersisted()
    {
        BotCommandManagementService service = new(DbContextFactory);
        BotCommandDefinition existingCommand = (await service.List(CancellationToken.None))
            .First(command => command.Trigger == "!discord");

        BotCommandDefinition? updatedCommand = await service.Update(
            existingCommand.Id,
            new BotCommandDefinitionInput("discord", "Fresh Discord invite", "Thiccdal.Bot.DiscordHandler", false),
            CancellationToken.None);

        Assert.NotNull(updatedCommand);
        Assert.Equal("!discord", updatedCommand.Trigger);
        Assert.Equal("Fresh Discord invite", updatedCommand.ResponseTemplate);
        Assert.Equal("Thiccdal.Bot.DiscordHandler", updatedCommand.HandlerType);
        Assert.False(updatedCommand.IsEnabled);
    }

    [Fact]
    public async Task WhenDeletingExistingCommand_ThenCommandIsRemoved()
    {
        BotCommandManagementService service = new(DbContextFactory);
        BotCommandDefinition command = (await service.List(CancellationToken.None))
            .First(existingCommand => existingCommand.Trigger == "!clip");

        bool deleted = await service.Delete(command.Id, CancellationToken.None);
        IReadOnlyList<BotCommandDefinition> remainingCommands = await service.List(CancellationToken.None);

        Assert.True(deleted);
        Assert.DoesNotContain(remainingCommands, existingCommand => existingCommand.Id == command.Id);
    }

    [Fact]
    public async Task WhenIncrementingUseCount_ThenPersistedCounterAdvances()
    {
        BotCommandManagementService service = new(DbContextFactory);

        await service.List(CancellationToken.None);
        await service.IncrementUseCount("!discord", CancellationToken.None);
        await service.IncrementUseCount("discord", CancellationToken.None);

        BotCommandDefinition command = (await service.List(CancellationToken.None))
            .First(existingCommand => existingCommand.Trigger == "!discord");

        Assert.Equal(2, command.UseCount);
    }
}
