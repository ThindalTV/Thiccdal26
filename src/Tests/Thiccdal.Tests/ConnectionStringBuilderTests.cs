using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Tests;

public sealed class ConnectionStringBuilderTests
{
    [Fact]
    public void GetDefaultPort_PostgreSQL_Returns5432()
    {
        var port = ConnectionStringBuilder.GetDefaultPort("PostgreSQL");

        Assert.Equal(5432, port);
    }

    [Fact]
    public void GetDefaultPort_SqlServer_Returns1433()
    {
        var port = ConnectionStringBuilder.GetDefaultPort("SqlServer");

        Assert.Equal(1433, port);
    }

    [Fact]
    public void GetDefaultPort_UnknownProvider_ReturnsZero()
    {
        var port = ConnectionStringBuilder.GetDefaultPort("Unknown");

        Assert.Equal(0, port);
    }

    [Fact]
    public void Build_PostgreSQL_ReturnsCorrectFormat()
    {
        var connectionString = ConnectionStringBuilder.Build(
            "PostgreSQL",
            "localhost",
            5432,
            "thiccdal",
            "postgres",
            "secret");

        Assert.Equal("Host=localhost;Port=5432;Database=thiccdal;Username=postgres;Password=secret", connectionString);
    }

    [Fact]
    public void Build_SqlServer_ReturnsCorrectFormat()
    {
        var connectionString = ConnectionStringBuilder.Build(
            "SqlServer",
            "localhost",
            1433,
            "thiccdal",
            "sa",
            "secret");

        Assert.Equal("Server=localhost,1433;Database=thiccdal;User Id=sa;Password=secret;TrustServerCertificate=True", connectionString);
    }

    [Fact]
    public void Build_WithZeroPort_UsesDefaultPort()
    {
        var connectionString = ConnectionStringBuilder.Build(
            "PostgreSQL",
            "localhost",
            0,
            "thiccdal",
            "postgres",
            "secret");

        Assert.Contains("Port=5432", connectionString);
    }

    [Fact]
    public void Build_WithCustomPort_UsesCustomPort()
    {
        var connectionString = ConnectionStringBuilder.Build(
            "PostgreSQL",
            "localhost",
            5433,
            "thiccdal",
            "postgres",
            "secret");

        Assert.Contains("Port=5433", connectionString);
    }

    [Fact]
    public void Build_UnknownProvider_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ConnectionStringBuilder.Build("MySQL", "localhost", 3306, "db", "user", "pass"));
    }

    [Fact]
    public void Validate_AllFieldsProvided_ReturnsTrue()
    {
        var result = ConnectionStringBuilder.Validate("localhost", "thiccdal", "postgres", out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingServer_ReturnsFalse(string? server)
    {
        var result = ConnectionStringBuilder.Validate(server!, "thiccdal", "postgres", out var error);

        Assert.False(result);
        Assert.Contains("Server", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingDatabase_ReturnsFalse(string? database)
    {
        var result = ConnectionStringBuilder.Validate("localhost", database!, "postgres", out var error);

        Assert.False(result);
        Assert.Contains("Database", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingUsername_ReturnsFalse(string? username)
    {
        var result = ConnectionStringBuilder.Validate("localhost", "thiccdal", username!, out var error);

        Assert.False(result);
        Assert.Contains("Username", error);
    }
}
