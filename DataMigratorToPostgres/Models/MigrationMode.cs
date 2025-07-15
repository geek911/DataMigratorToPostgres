namespace DataMigratorToPostgres.Models;

/// <summary>
/// Migration mode for handling existing data
/// </summary>
public enum MigrationMode
{
    /// <summary>
    /// Insert new data only (skip if table exists)
    /// </summary>
    Insert,

    /// <summary>
    /// Update existing data and insert new data
    /// </summary>
    Upsert,

    /// <summary>
    /// Drop and recreate target tables
    /// </summary>
    Overwrite,

    /// <summary>
    /// Truncate target tables before inserting
    /// </summary>
    Truncate
}