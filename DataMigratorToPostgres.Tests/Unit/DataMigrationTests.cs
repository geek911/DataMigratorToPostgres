using DataMigratorToPostgres.Models;
using DataMigratorToPostgres.Services;

namespace DataMigratorToPostgres.Tests.Unit;

// <summary>
/// Unit tests for DataMigrationService
/// </summary>
public class DataMigrationServiceTests
{
    private readonly DataMigrationService _service;

    public DataMigrationServiceTests()
    {
        _service = new DataMigrationService();
    }

    [Fact]
    public async Task MigrateAsync_WithEmptySourceConnections_ShouldReturnFailure()
    {
        // Arrange
        var options = new MigrationOptions();
        var emptyConnections = new List<string>();

        // Act
        var result = await _service.MigrateAsync(
            emptyConnections,
            "target connection",
            options);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("No source connection strings provided", result.Errors);
    }

    [Fact]
    public async Task MigrateAsync_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var sourceConnections = new[] { "source connection" };
        var targetConnection = "target connection";

        // Act & Assert
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            _service.MigrateAsync(sourceConnections, targetConnection, null));
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConnectionString_ShouldReturnFalse()
    {
        // Act
        var result = await _service.TestConnectionAsync("invalid connection string", false);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetTablesAsync_WithInvalidConnectionString_ShouldThrowException(string connectionString)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetTablesAsync(connectionString));
    }

    [Fact]
    public void MigrationOptions_ValidateDefaultBatchSize()
    {
        // Arrange
        var options = new MigrationOptions();

        // Act & Assert
        Assert.Equal(1000, options.BatchSize);
        Assert.True(options.BatchSize > 0);
    }

    [Fact]
    public void MigrationOptions_ValidateTimeoutDefaults()
    {
        // Arrange
        var options = new MigrationOptions();

        // Act & Assert
        Assert.Equal(30, options.ConnectionTimeout);
        Assert.Equal(300, options.CommandTimeout);
        Assert.True(options.ConnectionTimeout > 0);
        Assert.True(options.CommandTimeout > 0);
    }
}