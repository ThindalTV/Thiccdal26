using Thiccdal.Data;

namespace Thiccdal.Data.Tests;

public sealed class DatabaseProviderDetectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WhenConnectionStringIsNullOrEmpty_ThenReturnsSQLite(string? connectionString)
    {
        var result = DatabaseProviderDetector.Detect(connectionString!);

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.SQLite, result);
    }

    [Theory]
    [InlineData("Data Source=thiccdal.db")]
    [InlineData("Data Source=C:\\data\\app.db")]
    [InlineData("Data Source=myfile.db;")]
    public void WhenConnectionStringIsSQLiteFormat_ThenReturnsSQLite(string connectionString)
    {
        var result = DatabaseProviderDetector.Detect(connectionString);

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.SQLite, result);
    }

    [Theory]
    [InlineData("Host=localhost;Database=thiccdal;Username=postgres;Password=secret")]
    [InlineData("Host=db.example.com;Port=5432;Database=mydb;Username=user;Password=pass")]
    [InlineData("host=127.0.0.1;database=test")]
    public void WhenConnectionStringIsPostgreSQLFormat_ThenReturnsPostgreSQL(string connectionString)
    {
        var result = DatabaseProviderDetector.Detect(connectionString);

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.PostgreSQL, result);
    }

    [Theory]
    [InlineData("Server=localhost;Database=thiccdal;User Id=sa;Password=secret")]
    [InlineData("Server=db.example.com,1433;Database=mydb;User Id=user;Password=pass")]
    [InlineData("server=127.0.0.1;database=test;user id=admin;password=admin")]
    public void WhenConnectionStringIsSqlServerFormat_ThenReturnsSqlServer(string connectionString)
    {
        var result = DatabaseProviderDetector.Detect(connectionString);

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.SqlServer, result);
    }

    [Theory]
    [InlineData("Data Source=myserver;Initial Catalog=mydb;User Id=sa;Password=pass")]
    public void WhenConnectionStringUsesDataSourceWithoutDbExtension_ThenReturnsSqlServer(string connectionString)
    {
        var result = DatabaseProviderDetector.Detect(connectionString);

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.SqlServer, result);
    }

    [Fact]
    public void WhenConnectionStringIsUnknownFormat_ThenReturnsSQLite()
    {
        var result = DatabaseProviderDetector.Detect("SomeRandomString=value");

        Assert.Equal(DatabaseProviderDetector.DatabaseProvider.SQLite, result);
    }
}
