namespace DataMigratorToPostgres.Models;

/// <summary>
/// Result of a migration operation
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Whether the migration was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Migration start time
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Migration end time
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Total duration of migration
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Number of tables migrated
    /// </summary>
    public int TablesProcessed { get; set; }

    /// <summary>
    /// Total number of rows migrated
    /// </summary>
    public long TotalRowsMigrated { get; set; }

    /// <summary>
    /// Migration errors
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Migration warnings
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Detailed results per table
    /// </summary>
    public Dictionary<string, TableMigrationResult> TableResults { get; set; } = new();
}