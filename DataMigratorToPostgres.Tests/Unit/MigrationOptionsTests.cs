using DataMigratorToPostgres.Models;

namespace DataMigratorToPostgres.Tests.Unit;

/// <summary>
/// Unit tests for MigrationOptions
/// </summary>
public class MigrationOptionsTests
{
    [Fact]
    public void MigrationOptions_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new MigrationOptions();

        // Assert
        Assert.Equal(string.Empty, options.TablePrefix);
        Assert.Equal(MigrationMode.Insert, options.Mode);
        Assert.Equal(1000, options.BatchSize);
        Assert.True(options.CreateIndexes);
        Assert.False(options.CreateForeignKeys);
        Assert.Empty(options.ColumnMappings);
        Assert.Empty(options.ExcludeTables);
        Assert.Empty(options.IncludeTables);
        Assert.Equal(30, options.ConnectionTimeout);
        Assert.Equal(300, options.CommandTimeout);
    }

    [Fact]
    public void MigrationOptions_CanSetCustomValues()
    {
        // Arrange
        var options = new MigrationOptions
        {
            TablePrefix = "test_",
            Mode = MigrationMode.Overwrite,
            BatchSize = 500,
            CreateIndexes = false,
            CreateForeignKeys = true,
            ConnectionTimeout = 60,
            CommandTimeout = 600
        };

        // Act & Assert
        Assert.Equal("test_", options.TablePrefix);
        Assert.Equal(MigrationMode.Overwrite, options.Mode);
        Assert.Equal(500, options.BatchSize);
        Assert.False(options.CreateIndexes);
        Assert.True(options.CreateForeignKeys);
        Assert.Equal(60, options.ConnectionTimeout);
        Assert.Equal(600, options.CommandTimeout);
    }

    [Fact]
    public void MigrationOptions_Collections_CanBeModified()
    {
        // Arrange
        var options = new MigrationOptions();

        // Act
        options.ExcludeTables.Add("SystemTable");
        options.IncludeTables.Add("UserTable");
        options.ColumnMappings.Add("Table1", new Dictionary<string, string> { { "OldColumn", "NewColumn" } });

        // Assert
        Assert.Contains("SystemTable", options.ExcludeTables);
        Assert.Contains("UserTable", options.IncludeTables);
        Assert.True(options.ColumnMappings.ContainsKey("Table1"));
        Assert.Equal("NewColumn", options.ColumnMappings["Table1"]["OldColumn"]);
    }
}