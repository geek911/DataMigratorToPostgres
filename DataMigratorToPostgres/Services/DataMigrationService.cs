using System.Data;
using System.Diagnostics;
using System.Text;
using DataMigratorToPostgres.Models;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace DataMigratorToPostgres.Services;

/// <summary>
/// Service for migrating data from MSSQL to PostgreSQL
/// </summary>
public class DataMigrationService : IDataMigrationService
{
    private readonly Dictionary<Type, string> _typeMapping = new()
    {
        { typeof(int), "integer" },
        { typeof(long), "bigint" },
        { typeof(short), "smallint" },
        { typeof(byte), "smallint" },
        { typeof(bool), "boolean" },
        { typeof(decimal), "numeric" },
        { typeof(double), "double precision" },
        { typeof(float), "real" },
        { typeof(DateTime), "timestamp" },
        { typeof(DateTimeOffset), "timestamptz" },
        { typeof(Guid), "uuid" },
        { typeof(string), "text" },
        { typeof(byte[]), "bytea" }
    };

    /// <summary>
    /// Migrate data from single MSSQL source to PostgreSQL
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        string sourceConnectionString,
        string targetConnectionString,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        return await MigrateAsync(
            new[] { sourceConnectionString },
            targetConnectionString,
            options,
            cancellationToken);
    }

    /// <summary>
    /// Migrate data from multiple MSSQL sources to PostgreSQL
    /// </summary>
    public async Task<MigrationResult> MigrateAsync(
        IEnumerable<string> sourceConnectionStrings,
        string targetConnectionString,
        MigrationOptions options,
        CancellationToken cancellationToken = default)
    {

        if (options is null)
        {
            throw new NullReferenceException();
        }
        var result = new MigrationResult
        {
            StartTime = DateTime.UtcNow
        };

        try
        {
            var sourceConnections = sourceConnectionStrings.ToList();
            if (!sourceConnections.Any())
            {
                result.Errors.Add("No source connection strings provided");
                return result;
            }

            // Test connections
            foreach (var sourceConn in sourceConnections)
            {
                if (!await TestConnectionAsync(sourceConn, false, cancellationToken))
                {
                    result.Errors.Add($"Failed to connect to source database: {sourceConn}");
                    return result;
                }
            }

            if (!await TestConnectionAsync(targetConnectionString, true, cancellationToken))
            {
                result.Errors.Add("Failed to connect to target PostgreSQL database");
                return result;
            }

            // Get all tables from all sources
            var allTables = new Dictionary<string, string>(); // tableName -> connectionString
            foreach (var sourceConn in sourceConnections)
            {
                var tables = await GetTablesAsync(sourceConn, cancellationToken);
                foreach (var table in tables)
                {
                    if (ShouldMigrateTable(table, options))
                    {
                        allTables[table] = sourceConn;
                    }
                }
            }

            // Migrate each table
            foreach (var kvp in allTables)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var tableResult = await MigrateTableAsync(
                    kvp.Value,
                    targetConnectionString,
                    kvp.Key,
                    options,
                    cancellationToken);

                result.TableResults[kvp.Key] = tableResult;
                result.TablesProcessed++;
                result.TotalRowsMigrated += tableResult.RowsMigrated;

                if (!tableResult.Success)
                {
                    result.Errors.AddRange(tableResult.Errors);
                }
            }

            result.Success = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Migration failed: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            result.EndTime = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Get list of tables from MSSQL database
    /// </summary>
    public async Task<List<string>> GetTablesAsync(string connectionString,
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(connectionString)) throw new ArgumentException();
        
        var tables = new List<string>();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string query = @"
                SELECT TABLE_NAME 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_TYPE = 'BASE TABLE' 
                AND TABLE_SCHEMA = 'dbo'
                ORDER BY TABLE_NAME";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    public async Task<bool> TestConnectionAsync(string connectionString, bool isPostgreSQL,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (isPostgreSQL)
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return true;
            }
            else
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Migrate a single table
    /// </summary>
    private async Task<TableMigrationResult> MigrateTableAsync(
        string sourceConnectionString,
        string targetConnectionString,
        string tableName,
        MigrationOptions options,
        CancellationToken cancellationToken)
    {
        var result = new TableMigrationResult
        {
            SourceTableName = tableName,
            TargetTableName = $"{options.TablePrefix}{tableName}"
        };

        var startTime = DateTime.UtcNow;

        try
        {
            await using var sourceConnection = new SqlConnection(sourceConnectionString);
            await using var targetConnection = new NpgsqlConnection(targetConnectionString);

            await sourceConnection.OpenAsync(cancellationToken);
            await targetConnection.OpenAsync(cancellationToken);

            // Get table schema
            var columns = await GetTableSchemaAsync(sourceConnection, tableName, cancellationToken);
            if (!columns.Any())
            {
                result.Errors.Add($"No columns found for table {tableName}");
                return result;
            }
            
            // Handle existing data based on migration mode
            await HandleExistingDataAsync(targetConnection, result.TargetTableName, options.Mode, cancellationToken);
            // Create target table
            await CreateTargetTableAsync(targetConnection, result.TargetTableName, columns, options, cancellationToken);



            // Migrate data
            result.RowsMigrated = await MigrateTableDataAsync(
                sourceConnection,
                targetConnection,
                tableName,
                result.TargetTableName,
                columns,
                options,
                cancellationToken);

            result.Success = true;
        }
        catch (Exception ex ) when(ex.Message.Contains("does not exist"))
        {
            
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to migrate table {tableName}: {ex.Message}");
            result.Success = false;
        }
        finally
        {
            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }
    
    /// <summary>
    /// 
    /// </summary>
    public async Task<string?> GetPrimaryKeyAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string query = @"
        SELECT TOP 1 KU.COLUMN_NAME
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS TC
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS KU
            ON TC.CONSTRAINT_NAME = KU.CONSTRAINT_NAME
            AND TC.TABLE_SCHEMA = KU.TABLE_SCHEMA
        WHERE TC.TABLE_NAME = @TableName
          AND TC.CONSTRAINT_TYPE = 'PRIMARY KEY'
        ORDER BY KU.ORDINAL_POSITION;
    ";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }


    /// <summary>
    /// Get table schema from MSSQL
    /// </summary>
    private async Task<List<ColumnInfo>> GetTableSchemaAsync(SqlConnection connection, string tableName,
        CancellationToken cancellationToken)
    {
        
        var primaryKey = await GetPrimaryKeyAsync(connection, tableName, cancellationToken);

        var columns = new List<ColumnInfo>();

        const string query = @"
                SELECT 
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE,
                    COLUMN_DEFAULT,
                    ORDINAL_POSITION
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = @TableName
                ORDER BY ORDINAL_POSITION";

        await using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@TableName", tableName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString("COLUMN_NAME");

            columns.Add(new ColumnInfo
            {
                Name = columnName,
                DataType = reader.GetString("DATA_TYPE"),
                IsNullable = reader.GetString("IS_NULLABLE") == "YES",
                MaxLength = await reader.IsDBNullAsync("CHARACTER_MAXIMUM_LENGTH", cancellationToken)
                    ? null
                    : reader.GetInt32("CHARACTER_MAXIMUM_LENGTH"),
                Precision = await reader.IsDBNullAsync("NUMERIC_PRECISION", cancellationToken) ? null : reader.GetByte("NUMERIC_PRECISION"),
                Scale = await reader.IsDBNullAsync("NUMERIC_SCALE", cancellationToken) ? null : reader.GetInt32("NUMERIC_SCALE"),
                DefaultValue = await reader.IsDBNullAsync("COLUMN_DEFAULT", cancellationToken) ? null : reader.GetString("COLUMN_DEFAULT"),
                OrdinalPosition = reader.GetInt32("ORDINAL_POSITION"),
                IsPrimaryKey = string.Equals(columnName, primaryKey, StringComparison.OrdinalIgnoreCase)

            });
        }

        return columns;
    }

    /// <summary>
    /// Create target table in PostgreSQL
    /// </summary>
    private async Task CreateTargetTableAsync(
        NpgsqlConnection connection,
        string tableName,
        List<ColumnInfo> columns,
        MigrationOptions options,
        CancellationToken cancellationToken)
    {
        var sql = new StringBuilder();
        sql.AppendLine($"CREATE TABLE IF NOT EXISTS \"{tableName}\" (");

        var columnDefinitions = new List<string>();
        var primaryKeyColumns = columns
            .Where(c => c.IsPrimaryKey)
            .Select(c => $"\"{c.Name}\"")
            .ToList();

        foreach (var column in columns)
        {
            var pgType = MapSqlTypeToPostgreSQL(column);
            var nullable = column.IsNullable || column.IsPrimaryKey ? "" : " NOT NULL"; // PKs are always NOT NULL
            columnDefinitions.Add($"    \"{column.Name}\" {pgType}{nullable}");
        }

        if (primaryKeyColumns.Any())
        {
            columnDefinitions.Add($"    PRIMARY KEY ({string.Join(", ", primaryKeyColumns)})");
        }

        sql.AppendLine(string.Join(",\n", columnDefinitions));
        sql.AppendLine(");");

        await using var command = new NpgsqlCommand(sql.ToString(), connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Handle existing data based on migration mode
    /// </summary>
    private async Task HandleExistingDataAsync(
        NpgsqlConnection connection,
        string tableName,
        MigrationMode mode,
        CancellationToken cancellationToken)
    {
        switch (mode)
        {
            case MigrationMode.Overwrite:
                await ExecuteNonQueryAsync(connection, $"DROP TABLE IF EXISTS \"{tableName}\"", cancellationToken);
                break;
            case MigrationMode.Truncate:
                await ExecuteNonQueryAsync(connection, $"TRUNCATE TABLE \"{tableName}\"", cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Migrate table data
    /// </summary>
    private async Task<long> MigrateTableDataAsync(
        SqlConnection sourceConnection,
        NpgsqlConnection targetConnection,
        string sourceTableName,
        string targetTableName,
        List<ColumnInfo> columns,
        MigrationOptions options,
        CancellationToken cancellationToken)
    {
        var totalRows = 0L;
        var offset = 0;

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var batch = await GetBatchDataAsync(sourceConnection, sourceTableName, columns, offset, options.BatchSize,
                cancellationToken);
            if (!batch.Any())
                break;

            await InsertBatchAsync(targetConnection, targetTableName, columns, batch, cancellationToken);

            totalRows += batch.Count;
            offset += options.BatchSize;
        }

        return totalRows;
    }

    /// <summary>
    /// Get batch of data from source table
    /// </summary>
    private async Task<List<Dictionary<string, object>>> GetBatchDataAsync(
        SqlConnection connection,
        string tableName,
        List<ColumnInfo> columns,
        int offset,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var data = new List<Dictionary<string, object>>();
        var columnNames = string.Join(", ", columns.Select(c => $"[{c.Name}]"));

        var query = $@"
                SELECT {columnNames}
                FROM [{tableName}]
                ORDER BY (SELECT NULL)
                OFFSET {offset} ROWS
                FETCH NEXT {batchSize} ROWS ONLY";

        await using var command = new SqlCommand(query, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object>();
            foreach (var column in columns)
            {
                var value = reader.IsDBNull(column.Name) ? null : reader.GetValue(column.Name);
                row[column.Name] = value;
            }

            data.Add(row);
        }

        return data;
    }

    /// <summary>
    /// Insert batch of data into target table
    /// </summary>
    private async Task InsertBatchAsync(
        NpgsqlConnection connection,
        string tableName,
        List<ColumnInfo> columns,
        List<Dictionary<string, object>> batch,
        CancellationToken cancellationToken)
    {
        if (!batch.Any()) return;

        var columnNames = string.Join(", ", columns.Select(c => $"\"{c.Name}\""));
        var parameterNames = string.Join(", ", columns.Select((c, i) => $"${i + 1}"));

        var sql = $"INSERT INTO \"{tableName}\" ({columnNames}) VALUES ({parameterNames})";

        await using var command = new NpgsqlCommand(sql, connection);

        foreach (var row in batch)
        {
            command.Parameters.Clear();

            foreach (var column in columns)
            {
                var value = row.ContainsKey(column.Name) ? row[column.Name] : null;
                command.Parameters.AddWithValue(ConvertValueForPostgreSQL(value));
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Map SQL Server data type to PostgreSQL
    /// </summary>
    private string MapSqlTypeToPostgreSQL(ColumnInfo column)
    {
        return column.DataType.ToLower() switch
        {
            "int" => "integer",
            "bigint" => "bigint",
            "smallint" => "smallint",
            "tinyint" => "smallint",
            "bit" => "boolean",
            "decimal" or "numeric" => $"numeric({column.Precision},{column.Scale})",
            "money" => "money",
            "float" => "double precision",
            "real" => "real",
            "datetime" or "datetime2" => "timestamp",
            "smalldatetime" => "timestamp",
            "date" => "date",
            "time" => "time",
            "datetimeoffset" => "timestamptz",
            "uniqueidentifier" => "uuid",
            "varchar" => column.MaxLength.HasValue ? $"varchar({column.MaxLength})" : "text",
            "nvarchar" => column.MaxLength.HasValue ? $"varchar({column.MaxLength})" : "text",
            "char" => column.MaxLength.HasValue ? $"char({column.MaxLength})" : "text",
            "nchar" => column.MaxLength.HasValue ? $"char({column.MaxLength})" : "text",
            "text" or "ntext" => "text",
            "binary" or "varbinary" => "bytea",
            "image" => "bytea",
            "xml" => "xml",
            _ => "text"
        };
    }

    /// <summary>
    /// Convert value for PostgreSQL insertion
    /// </summary>
    private object ConvertValueForPostgreSQL(object value)
    {
        if (value == null || value == DBNull.Value)
            return DBNull.Value;

        return value switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.ToUniversalTime(),
            Guid guid => guid,
            byte[] bytes => bytes,
            _ => value
        };
    }

    /// <summary>
    /// Execute non-query command
    /// </summary>
    private async Task ExecuteNonQueryAsync(NpgsqlConnection connection, string sql,
        CancellationToken cancellationToken)
    {
        using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Check if table should be migrated based on options
    /// </summary>
    private bool ShouldMigrateTable(string tableName, MigrationOptions options)
    {
        if (options.ExcludeTables.Contains(tableName))
            return false;

        if (options.IncludeTables.Any() && !options.IncludeTables.Contains(tableName))
            return false;

        return true;
    }

    /// <summary>
    /// Column information
    /// </summary>
    private class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public int? MaxLength { get; set; }
        public byte? Precision { get; set; }
        public int? Scale { get; set; }
        public string? DefaultValue { get; set; }
        public int OrdinalPosition { get; set; }
        
        public bool IsPrimaryKey { get; set; }  
    }
}

