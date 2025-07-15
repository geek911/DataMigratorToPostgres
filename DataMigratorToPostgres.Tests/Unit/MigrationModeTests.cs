using DataMigratorToPostgres.Models;

namespace DataMigratorToPostgres.Tests.Unit;

/// <summary>
/// Unit tests for MigrationMode enum
/// </summary>
public class MigrationModeTests
{
    [Fact]
    public void MigrationMode_ShouldHaveExpectedValues()
    {
        // Act & Assert
        Assert.Equal(0, (int)MigrationMode.Insert);
        Assert.Equal(1, (int)MigrationMode.Upsert);
        Assert.Equal(2, (int)MigrationMode.Overwrite);
        Assert.Equal(3, (int)MigrationMode.Truncate);
    }

    [Fact]
    public void MigrationMode_ShouldHaveCorrectNames()
    {
        // Act & Assert
        Assert.Equal(nameof(MigrationMode.Insert), MigrationMode.Insert.ToString());
        Assert.Equal(nameof(MigrationMode.Upsert), MigrationMode.Upsert.ToString());
        Assert.Equal(nameof(MigrationMode.Overwrite), MigrationMode.Overwrite.ToString());
        Assert.Equal(nameof(MigrationMode.Truncate), MigrationMode.Truncate.ToString());
    }
}