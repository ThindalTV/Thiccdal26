using Microsoft.Extensions.Logging.Abstractions;

namespace Thiccdal.Data.Tests;

public sealed class ConfigurationPersistenceServiceTests : IAsyncDisposable
{
    private readonly InMemoryApplicationDbContextFactory _contextFactory;
    private readonly ConfigurationPersistenceService _service;

    public ConfigurationPersistenceServiceTests()
    {
        _contextFactory = new InMemoryApplicationDbContextFactory();
        _service = new ConfigurationPersistenceService(
            _contextFactory,
            NullLogger<ConfigurationPersistenceService>.Instance);
    }

    public ValueTask DisposeAsync() => _contextFactory.DisposeAsync();

    [Fact]
    public async Task WhenKeyDoesNotExist_ThenGetValueReturnsNull()
    {
        var value = await _service.GetValue("nonexistent");

        Assert.Null(value);
    }

    [Fact]
    public async Task WhenValueIsSet_ThenGetValueReturnsIt()
    {
        await _service.SetValue("TestKey", "TestValue");

        var value = await _service.GetValue("TestKey");

        Assert.Equal("TestValue", value);
    }

    [Fact]
    public async Task WhenValueIsUpdated_ThenNewValueIsPersisted()
    {
        await _service.SetValue("TestKey", "OldValue");
        await _service.SetValue("TestKey", "NewValue");

        var value = await _service.GetValue("TestKey");

        Assert.Equal("NewValue", value);
    }

    [Fact]
    public async Task WhenKeyExists_ThenHasKeyReturnsTrue()
    {
        await _service.SetValue("TestKey", "TestValue");

        var exists = await _service.HasKey("TestKey");

        Assert.True(exists);
    }

    [Fact]
    public async Task WhenKeyDoesNotExist_ThenHasKeyReturnsFalse()
    {
        var exists = await _service.HasKey("nonexistent");

        Assert.False(exists);
    }

    [Fact]
    public async Task WhenKeyIsRemoved_ThenGetValueReturnsNull()
    {
        await _service.SetValue("TestKey", "TestValue");
        await _service.RemoveKey("TestKey");

        var value = await _service.GetValue("TestKey");

        Assert.Null(value);
    }

    [Fact]
    public async Task WhenRemovingNonexistentKey_ThenNoExceptionIsThrown()
    {
        await _service.RemoveKey("nonexistent");

        var exists = await _service.HasKey("nonexistent");
        Assert.False(exists);
    }

    [Fact]
    public async Task WhenTypedValueIsSet_ThenItCanBeRetrieved()
    {
        var testConfig = new TestConfig { Name = "Test", Count = 42 };

        await _service.SetValue("TypedKey", testConfig);
        var retrieved = await _service.GetValue<TestConfig>("TypedKey");

        Assert.NotNull(retrieved);
        Assert.Equal("Test", retrieved.Name);
        Assert.Equal(42, retrieved.Count);
    }

    [Fact]
    public async Task WhenTypedKeyDoesNotExist_ThenGetValueReturnsNull()
    {
        var value = await _service.GetValue<TestConfig>("nonexistent");

        Assert.Null(value);
    }

    [Fact]
    public async Task WhenJsonIsInvalid_ThenGetValueReturnsNull()
    {
        await _service.SetValue("BadJson", "not valid json {{{");

        var value = await _service.GetValue<TestConfig>("BadJson");

        Assert.Null(value);
    }

    private sealed class TestConfig
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
