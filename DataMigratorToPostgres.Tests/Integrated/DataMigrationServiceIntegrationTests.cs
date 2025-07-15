using DataMigratorToPostgres.Models;
using DataMigratorToPostgres.Services;
using Microsoft.Data.SqlClient;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace DataMigratorToPostgres.Tests.Integrated;

// <summary>
/// Integration tests using test containers
/// </summary>
public class DataMigrationServiceIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _mssqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
        .WithPassword("YourStrong@Passw0rd")
        .Build();
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("testdb")
        .WithUsername("testuser")
        .WithPassword("testpass")
        .Build();
    private readonly DataMigrationService _migrationService = new();

    public async Task InitializeAsync()
    {
        await _mssqlContainer.StartAsync();
        await _postgresContainer.StartAsync();
        await SetupTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _mssqlContainer.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task MigrateAsync_WithValidData_ShouldMigrateSuccessfully()
    {
        // Arrange
        var options = new MigrationOptions
        {
            TablePrefix = "migrated_",
            Mode = MigrationMode.Insert,
            BatchSize = 100
        };

        // Act
        var result = await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.TablesProcessed > 0);
        Assert.True(result.TotalRowsMigrated > 0);
        Assert.Empty(result.Errors);

        // Verify data was migrated
        await using var pgConnection = new NpgsqlConnection(_postgresContainer.GetConnectionString());
        await pgConnection.OpenAsync();

        var command = new NpgsqlCommand("SELECT COUNT(*) FROM migrated_users", pgConnection);
        var count = (long)(await command.ExecuteScalarAsync() ?? 0);

        Assert.True(count > 0);
    }

    [Fact]
    public async Task MigrateAsync_WithTablePrefix_ShouldCreateTablesWithPrefix()
    {
        // Arrange
        var options = new MigrationOptions
        {
            TablePrefix = "test_prefix_",
            Mode = MigrationMode.Insert
        };

        // Act
        var result = await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Assert
        Assert.True(result.Success);

        // Verify table exists with prefix
        await using var pgConnection = new NpgsqlConnection(_postgresContainer.GetConnectionString());
        await pgConnection.OpenAsync();

        var command = new NpgsqlCommand(@"
                SELECT COUNT(*) 
                FROM information_schema.tables 
                WHERE table_name = 'test_prefix_users'", pgConnection);
        var count = (long)(await command.ExecuteScalarAsync() ?? 0);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MigrateAsync_WithOverwriteMode_ShouldReplaceExistingData()
    {
        // Arrange
        var options = new MigrationOptions
        {
            Mode = MigrationMode.Overwrite,
            BatchSize = 50
        };
        
        

        // First migration
        await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Add more data to source
        await AddMoreTestDataAsync();

        // Act - Second migration with overwrite
        var result = await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.TotalRowsMigrated > 0);
    }

    [Fact]
    public async Task MigrateAsync_WithExcludeTables_ShouldSkipExcludedTables()
    {
        // Arrange
        var options = new MigrationOptions
        {
            ExcludeTables = new HashSet<string> { "users" },
            Mode = MigrationMode.Insert
        };

        // Act
        var result = await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.TableResults.ContainsKey("users"));
    }

    [Fact]
    public async Task MigrateAsync_WithIncludeTables_ShouldOnlyMigrateIncludedTables()
    {
        // Arrange
        var options = new MigrationOptions
        {
            IncludeTables = new HashSet<string> { "users" },
            Mode = MigrationMode.Insert
        };

        // Act
        var result = await _migrationService.MigrateAsync(
            _mssqlContainer.GetConnectionString(),
            _postgresContainer.GetConnectionString(),
            options);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.TableResults.ContainsKey("users"));
        Assert.Single(result.TableResults);
    }

    [Fact]
    public async Task GetTablesAsync_ShouldReturnAllTables()
    {
        // Act
        var tables = await _migrationService.GetTablesAsync(_mssqlContainer.GetConnectionString());

        // Assert
        Assert.NotEmpty(tables);
        Assert.Contains("users", tables);
        Assert.Contains("orders", tables);
    }

    [Fact]
    public async Task TestConnectionAsync_WithValidConnections_ShouldReturnTrue()
    {
        // Act
        var mssqlResult = await _migrationService.TestConnectionAsync(_mssqlContainer.GetConnectionString(), false);
        var postgresResult =
            await _migrationService.TestConnectionAsync(_postgresContainer.GetConnectionString(), true);

        // Assert
        Assert.True(mssqlResult);
        Assert.True(postgresResult);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConnection_ShouldReturnFalse()
    {
        // Act
        var result = await _migrationService.TestConnectionAsync("invalid connection string", false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task MigrateAsync_WithMultipleSources_ShouldMigrateFromAllSources()
    {
        // Arrange
        var secondMssqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
            .WithPassword("YourStrong@Passw0rd2")
            .Build();

        await secondMssqlContainer.StartAsync();

        try
        {
            await SetupSecondTestDataAsync(secondMssqlContainer.GetConnectionString());

            var options = new MigrationOptions
            {
                TablePrefix = "multi_",
                Mode = MigrationMode.Insert
            };

            var sourceConnections = new[]
            {
                _mssqlContainer.GetConnectionString(),
                secondMssqlContainer.GetConnectionString()
            };

            // Act
            var result = await _migrationService.MigrateAsync(
                sourceConnections,
                _postgresContainer.GetConnectionString(),
                options);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.TablesProcessed > 0);
            Assert.True(result.TotalRowsMigrated > 0);
        }
        finally
        {
            await secondMssqlContainer.DisposeAsync();
        }
    }

    private async Task SetupTestDataAsync()
    {
        await using var connection = new SqlConnection(_mssqlContainer.GetConnectionString());
        await connection.OpenAsync();

        // Create test tables
        var createUsersTable = @"
                CREATE TABLE users (
                    id INT PRIMARY KEY IDENTITY(1,1),
                    username NVARCHAR(50) NOT NULL,
                    email NVARCHAR(100),
                    created_date DATETIME2 DEFAULT GETDATE(),
                    is_active BIT DEFAULT 1,
                    age INT,
                    balance DECIMAL(10,2),
                    user_guid UNIQUEIDENTIFIER DEFAULT NEWID()
                )";

        var createOrdersTable = @"
                CREATE TABLE orders (
                    id INT PRIMARY KEY IDENTITY(1,1),
                    user_id INT,
                    order_date DATETIME2 DEFAULT GETDATE(),
                    total_amount DECIMAL(10,2),
                    status NVARCHAR(20) DEFAULT 'pending'
                )";

        await using var cmd1 = new SqlCommand(createUsersTable, connection);
        await cmd1.ExecuteNonQueryAsync();

        await using var cmd2 = new SqlCommand(createOrdersTable, connection);
        await cmd2.ExecuteNonQueryAsync();

        // Insert test data
        var insertUsers = @"
                INSERT INTO users (username, email, age, balance) VALUES 
                ('user1', 'user1@example.com', 25, 1000.50),
                ('user2', 'user2@example.com', 30, 2000.75),
                ('user3', 'user3@example.com', 28, 1500.25)";

        var insertOrders = @"
                INSERT INTO orders (user_id, total_amount, status) VALUES 
                (1, 100.00, 'completed'),
                (2, 250.50, 'pending'),
                (1, 75.25, 'completed')";

        await using var cmd3 = new SqlCommand(insertUsers, connection);
        await cmd3.ExecuteNonQueryAsync();

        await using var cmd4 = new SqlCommand(insertOrders, connection);
        await cmd4.ExecuteNonQueryAsync();
    }

    private async Task AddMoreTestDataAsync()
    {
        await using var connection = new SqlConnection(_mssqlContainer.GetConnectionString());
        await connection.OpenAsync();

        var insertMoreUsers = @"
                INSERT INTO users (username, email, age, balance) VALUES 
                ('user4', 'user4@example.com', 35, 3000.00),
                ('user5', 'user5@example.com', 22, 500.00)";

        await using var cmd = new SqlCommand(insertMoreUsers, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetupSecondTestDataAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // Create test table in second database
        var createProductsTable = @"
                CREATE TABLE products (
                    id INT PRIMARY KEY IDENTITY(1,1),
                    name NVARCHAR(100) NOT NULL,
                    price DECIMAL(10,2),
                    category NVARCHAR(50),
                    in_stock BIT DEFAULT 1
                )";

        await using var cmd1 = new SqlCommand(createProductsTable, connection);
        await cmd1.ExecuteNonQueryAsync();

        // Insert test data
        var insertProducts = @"
                INSERT INTO products (name, price, category) VALUES 
                ('Product 1', 19.99, 'Electronics'),
                ('Product 2', 29.99, 'Books'),
                ('Product 3', 39.99, 'Clothing')";

        await using var cmd2 = new SqlCommand(insertProducts, connection);
        await cmd2.ExecuteNonQueryAsync();
    }
    
    [Fact(DisplayName = "Detects primary key from MSSQL table")]
    public async Task GetPrimaryKeyAsync_ReturnsPrimaryKeyColumn()
    {
        var cancellationToken = CancellationToken.None;

        await using var connection = new SqlConnection(_mssqlContainer.GetConnectionString());
        await connection.OpenAsync(cancellationToken);

        // Create table with a primary key if not done already
        const string createTableSql = @"
        IF OBJECT_ID('dbo.TestTable', 'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TestTable (
                Id INT PRIMARY KEY,
                Name NVARCHAR(100)
            );
        END
    ";
        await using var createCmd = new SqlCommand(createTableSql, connection);
        await createCmd.ExecuteNonQueryAsync(cancellationToken);

        // Call your actual method
        var pkColumn = await _migrationService.GetPrimaryKeyAsync(connection, "TestTable", cancellationToken);

        // Assert
        Assert.NotNull(pkColumn);
        Assert.Equal("Id", pkColumn);
    }

}