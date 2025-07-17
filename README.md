# MSSQL to PostgreSQL Migration Library

[![Buy Me a Coffee](https://img.shields.io/badge/☕-Buy%20me%20a%20coffee-yellow)](https://coff.ee/geek911)

A comprehensive .NET library for migrating data from Microsoft SQL Server to PostgreSQL databases with support for multiple sources, dynamic table creation, and flexible configuration options.

## Features

- **Multiple Source Support**: Migrate from one or multiple MSSQL databases
- **Dynamic Table Creation**: Automatically creates PostgreSQL tables based on MSSQL schema
- **Flexible Migration Modes**: Insert, Upsert, Overwrite, or Truncate existing data
- **Table Prefixing**: Add custom prefixes to target table names
- **Batch Processing**: Configurable batch sizes for optimal performance
- **Schema Mapping**: Automatic data type mapping from MSSQL to PostgreSQL
- **Connection Testing**: Built-in connection validation
- **Comprehensive Logging**: Detailed migration results and error reporting
- **Unit Testing**: Full test coverage with integration tests using TestContainers

## Installation

```bash
dotnet add package DataMigratorToPostgres
```

## Quick Start

```csharp
using DataMigratorToPostgres.Models;
using DataMigratorToPostgres.Services;

var migrationService = new DataMigrationService();

var options = new MigrationOptions
{
    TablePrefix = "migrated_",
    Mode = MigrationMode.Insert,
    BatchSize = 1000
};

var result = await migrationService.MigrateAsync(
    "Server=localhost;Database=SourceDB;Trusted_Connection=true;",
    "Host=localhost;Database=TargetDB;Username=postgres;Password=password;",
    options);

Console.WriteLine($"Migration {(result.Success ? "successful" : "failed")}");
Console.WriteLine($"Migrated {result.TotalRowsMigrated} rows in {result.Duration}");
```

## Configuration Options

### MigrationOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `TablePrefix` | `string` | `""` | Prefix to add to target table names |
| `Mode` | `MigrationMode` | `Insert` | Migration mode for handling existing data |
| `BatchSize` | `int` | `1000` | Number of rows to process in each batch |
| `CreateIndexes` | `bool` | `true` | Whether to create indexes on target tables |
| `CreateForeignKeys` | `bool` | `false` | Whether to create foreign key constraints |
| `ExcludeTables` | `HashSet<string>` | `empty` | Tables to exclude from migration |
| `IncludeTables` | `HashSet<string>` | `empty` | Tables to include (if empty, all tables) |
| `ConnectionTimeout` | `int` | `30` | Connection timeout in seconds |
| `CommandTimeout` | `int` | `300` | Command timeout in seconds |

### Migration Modes

- **Insert**: Insert new data only (skip if table exists)
- **Upsert**: Update existing data and insert new data (requires primary key)
- **Overwrite**: Drop and recreate target tables
- **Truncate**: Clear target tables before inserting

## Data Type Mapping

The library automatically maps MSSQL data types to PostgreSQL equivalents:

| MSSQL Type | PostgreSQL Type |
|------------|----------------|
| `int` | `integer` |
| `bigint` | `bigint` |
| `varchar(n)` | `varchar(n)` |
| `nvarchar(n)` | `varchar(n)` |
| `datetime2` | `timestamp` |
| `uniqueidentifier` | `uuid` |
| `decimal(p,s)` | `numeric(p,s)` |
| `bit` | `boolean` |
| `varbinary` | `bytea` |

## Multiple Source Migration

```csharp
var sourceConnections = new[]
{
    "Server=server1;Database=DB1;Trusted_Connection=true;",
    "Server=server2;Database=DB2;Trusted_Connection=true;"
};

var result = await migrationService.MigrateAsync(
    sourceConnections,
    targetConnectionString,
    options);
```

## Advanced Usage

### Table Filtering

```csharp
var options = new MigrationOptions
{
    // Exclude system tables
    ExcludeTables = new HashSet<string> { "SystemLog", "TempData" },
    
    // Only migrate specific tables
    IncludeTables = new HashSet<string> { "Users", "Orders", "Products" }
};
```

### Custom Column Mappings

```csharp
var options = new MigrationOptions
{
    ColumnMappings = new Dictionary<string, Dictionary<string, string>>
    {
        ["Users"] = new Dictionary<string, string>
        {
            ["user_id"] = "id",
            ["user_name"] = "username"
        }
    }
};
```

### Connection Testing

```csharp
var sourceValid = await migrationService.TestConnectionAsync(sourceConnectionString, false);
var targetValid = await migrationService.TestConnectionAsync(targetConnectionString, true);

if (!sourceValid || !targetValid)
{
    Console.WriteLine("Connection test failed");
    return;
}
```

## Error Handling

The library provides comprehensive error reporting:

```csharp
var result = await migrationService.MigrateAsync(/* ... */);

if (!result.Success)
{
    Console.WriteLine("Migration failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"- {error}");
    }
}

// Check individual table results
foreach (var tableResult in result.TableResults.Values)
{
    if (!tableResult.Success)
    {
        Console.WriteLine($"Table {tableResult.SourceTableName} failed:");
        foreach (var error in tableResult.Errors)
        {
            Console.WriteLine($"  - {error}");
        }
    }
}
```

## Testing

The library includes comprehensive unit and integration tests using TestContainers:

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter "Category=Unit"

# Run only integration tests
dotnet test --filter "Category=Integration"
```

## Performance Considerations

- **Batch Size**: Adjust based on available memory and network latency
- **Connection Pooling**: Enable connection pooling for better performance
- **Indexes**: Consider disabling index creation during migration for faster inserts
- **Parallel Processing**: The library processes tables sequentially by design for data consistency

## License

This project is licensed under the MIT License.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
