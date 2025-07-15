namespace DataMigratorToPostgres.Models;

// <summary>
/// Configuration options for data migration
/// </summary>
public class MigrationOptions
{
    /// <summary>
    /// Prefix to add to target table names
    /// </summary>
    public string TablePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Migration mode for handling existing data
    /// </summary>
    public MigrationMode Mode { get; set; } = MigrationMode.Insert;

    /// <summary>
    /// Batch size for data transfer
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Whether to create indexes on target tables
    /// </summary>
    public bool CreateIndexes { get; set; } = true;

    /// <summary>
    /// Whether to create foreign key constraints
    /// </summary>
    public bool CreateForeignKeys { get; set; } = false;

    /// <summary>
    /// Custom column mappings (source column -> target column)
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> ColumnMappings { get; set; } = new();

    /// <summary>
    /// Tables to exclude from migration
    /// </summary>
    public HashSet<string> ExcludeTables { get; set; } = new();

    /// <summary>
    /// Tables to include in migration (if empty, all tables are included)
    /// </summary>
    public HashSet<string> IncludeTables { get; set; } = new();

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeout { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeout { get; set; } = 300;
}