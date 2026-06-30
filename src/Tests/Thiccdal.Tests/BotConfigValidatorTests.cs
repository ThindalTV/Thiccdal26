using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class BotConfigValidatorTests
{
    [Theory]
    [InlineData("Thiccdal")]
    [InlineData("Bot")]
    [InlineData("MyStreamBot123")]
    public void ValidateBotName_ValidName_ReturnsTrue(string botName)
    {
        var result = BotConfigValidator.ValidateBotName(botName, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateBotName_NullOrEmpty_ReturnsFalse(string? botName)
    {
        var result = BotConfigValidator.ValidateBotName(botName, out var error);

        Assert.False(result);
        Assert.Contains("required", error);
    }

    [Fact]
    public void ValidateBotName_TooShort_ReturnsFalse()
    {
        var result = BotConfigValidator.ValidateBotName("X", out var error);

        Assert.False(result);
        Assert.Contains("at least", error);
    }

    [Fact]
    public void ValidateBotName_TooLong_ReturnsFalse()
    {
        var longName = new string('X', BotConfigValidator.MaxBotNameLength + 1);

        var result = BotConfigValidator.ValidateBotName(longName, out var error);

        Assert.False(result);
        Assert.Contains("exceed", error);
    }

    [Fact]
    public void ValidateBotName_ContainsSpaces_ReturnsFalse()
    {
        var result = BotConfigValidator.ValidateBotName("My Bot", out var error);

        Assert.False(result);
        Assert.Contains("spaces", error);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    public void ValidateTimedMessageInterval_ValidInterval_ReturnsTrue(int interval)
    {
        var result = BotConfigValidator.ValidateTimedMessageInterval(interval, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateTimedMessageInterval_TooShort_ReturnsFalse()
    {
        var result = BotConfigValidator.ValidateTimedMessageInterval(1, out var error);

        Assert.False(result);
        Assert.Contains("at least", error);
    }

    [Fact]
    public void ValidateTimedMessageInterval_TooLong_ReturnsFalse()
    {
        var result = BotConfigValidator.ValidateTimedMessageInterval(120, out var error);

        Assert.False(result);
        Assert.Contains("exceed", error);
    }

    [Fact]
    public void InterpolateTemplate_ReplacesUsername()
    {
        var template = "Welcome, {{username}}!";

        var result = BotConfigValidator.InterpolateTemplate(template, "TestUser");

        Assert.Equal("Welcome, TestUser!", result);
    }

    [Fact]
    public void InterpolateTemplate_ReplacesUsernameAndTier()
    {
        var template = "Thanks {{username}} for the {{tier}} sub!";

        var result = BotConfigValidator.InterpolateTemplate(template, "TestUser", "Tier 3");

        Assert.Equal("Thanks TestUser for the Tier 3 sub!", result);
    }

    [Fact]
    public void InterpolateTemplate_IsCaseInsensitive()
    {
        var template = "Hello {{USERNAME}}, welcome {{UserName}}!";

        var result = BotConfigValidator.InterpolateTemplate(template, "TestUser");

        Assert.Equal("Hello TestUser, welcome TestUser!", result);
    }

    [Fact]
    public void InterpolateTemplate_WithNullTier_LeavesPlaceholder()
    {
        var template = "Thanks for the {{tier}} sub!";

        var result = BotConfigValidator.InterpolateTemplate(template, "TestUser", null);

        Assert.Contains("{{tier}}", result);
    }

    [Fact]
    public void InterpolateTemplate_NoPlaceholders_ReturnsOriginal()
    {
        var template = "Welcome to the stream!";

        var result = BotConfigValidator.InterpolateTemplate(template, "TestUser");

        Assert.Equal("Welcome to the stream!", result);
    }
}
