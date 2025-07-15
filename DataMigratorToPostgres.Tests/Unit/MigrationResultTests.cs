using DataMigratorToPostgres.Models;

namespace DataMigratorToPostgres.Tests.Unit;

/// <summary>
/// Unit tests for MigrationResult
/// </summary>
public class MigrationResultTests
{
    [Fact]
    public void MigrationResult_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var result = new MigrationResult();

        // Assert
        Assert.False(result.Success);
        Assert.Equal(0, result.TablesProcessed);
        Assert.Equal(0, result.TotalRowsMigrated);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
        Assert.Empty(result.TableResults);
    }

    [Fact]
    public void MigrationResult_Duration_CalculatesCorrectly()
    {
        // Arrange
        var result = new MigrationResult
        {
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            EndTime = DateTime.UtcNow
        };

        // Act
        var duration = result.Duration;

        // Assert
        Assert.True(duration.TotalMinutes >= 4.5);
        Assert.True(duration.TotalMinutes <= 5.5);
    }

    [Fact]
    public void TableMigrationResult_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var result = new TableMigrationResult();

        // Assert
        Assert.Equal(string.Empty, result.SourceTableName);
        Assert.Equal(string.Empty, result.TargetTableName);
        Assert.Equal(0, result.RowsMigrated);
        Assert.False(result.Success);
        Assert.Equal(TimeSpan.Zero, result.Duration);
        Assert.Empty(result.Errors);
    }
}