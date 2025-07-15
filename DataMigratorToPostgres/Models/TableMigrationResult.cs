namespace DataMigratorToPostgres.Models;

/// <summary>
/// Result of migrating a single table
/// </summary>
public class TableMigrationResult
{
    private string _targetTableName = string.Empty;

    /// <summary>
    /// Source table name
    /// </summary>
    public string SourceTableName { get; set; } = string.Empty;

    /// <summary>
    /// Target table name
    /// </summary>

    public string TargetTableName
    {
        get => _targetTableName;
        set => _targetTableName = value.ToLower();
    }

    /// <summary>
    /// Number of rows migrated
    /// </summary>
    public long RowsMigrated { get; set; }

    /// <summary>
    /// Whether the table migration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Migration duration for this table
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Any errors specific to this table
    /// </summary>
    public List<string> Errors { get; set; } = new();
}