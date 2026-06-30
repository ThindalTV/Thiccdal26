using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class AiConfigValidatorTests
{
    [Theory]
    [InlineData("http://localhost:1234/v1")]
    [InlineData("https://api.openai.com/v1")]
    [InlineData("http://192.168.1.100:8080")]
    public void ValidateEndpoint_ValidUrl_ReturnsTrue(string endpoint)
    {
        var result = AiConfigValidator.ValidateEndpoint(endpoint, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateEndpoint_NullOrEmpty_ReturnsFalse(string? endpoint)
    {
        var result = AiConfigValidator.ValidateEndpoint(endpoint, out var error);

        Assert.False(result);
        Assert.Contains("required", error);
    }

    [Fact]
    public void ValidateEndpoint_InvalidUrl_ReturnsFalse()
    {
        var result = AiConfigValidator.ValidateEndpoint("not-a-url", out var error);

        Assert.False(result);
        Assert.Contains("valid URL", error);
    }

    [Fact]
    public void ValidateEndpoint_NonHttpScheme_ReturnsFalse()
    {
        var result = AiConfigValidator.ValidateEndpoint("ftp://example.com", out var error);

        Assert.False(result);
        Assert.Contains("HTTP", error);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(30)]
    [InlineData(300)]
    public void ValidateTimeout_ValidTimeout_ReturnsTrue(int timeout)
    {
        var result = AiConfigValidator.ValidateTimeout(timeout, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateTimeout_TooShort_ReturnsFalse()
    {
        var result = AiConfigValidator.ValidateTimeout(1, out var error);

        Assert.False(result);
        Assert.Contains("at least", error);
    }

    [Fact]
    public void ValidateTimeout_TooLong_ReturnsFalse()
    {
        var result = AiConfigValidator.ValidateTimeout(600, out var error);

        Assert.False(result);
        Assert.Contains("exceed", error);
    }

    [Theory]
    [InlineData("http://localhost:1234/v1/", "http://localhost:1234/v1")]
    [InlineData("http://localhost:1234/v1", "http://localhost:1234/v1")]
    [InlineData("https://api.openai.com///", "https://api.openai.com")]
    public void NormalizeEndpoint_RemovesTrailingSlashes(string input, string expected)
    {
        var result = AiConfigValidator.NormalizeEndpoint(input);

        Assert.Equal(expected, result);
    }
}
