using DataMigratorToPostgres.Models;

namespace DataMigratorToPostgres.Services;

/// <summary>
    /// Interface for data migration services
    /// </summary>
    public interface IDataMigrationService
    {
        /// <summary>
        /// Migrate data from MSSQL to PostgreSQL
        /// </summary>
        /// <param name="sourceConnectionStrings">Source MSSQL connection strings</param>
        /// <param name="targetConnectionString">Target PostgreSQL connection string</param>
        /// <param name="options">Migration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Migration result</returns>
        Task<MigrationResult> MigrateAsync(
            IEnumerable<string> sourceConnectionStrings,
            string targetConnectionString,
            MigrationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Migrate data from single MSSQL source to PostgreSQL
        /// </summary>
        /// <param name="sourceConnectionString">Source MSSQL connection string</param>
        /// <param name="targetConnectionString">Target PostgreSQL connection string</param>
        /// <param name="options">Migration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Migration result</returns>
        Task<MigrationResult> MigrateAsync(
            string sourceConnectionString,
            string targetConnectionString,
            MigrationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get list of tables from MSSQL source
        /// </summary>
        /// <param name="connectionString">MSSQL connection string</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of table names</returns>
        Task<List<string>> GetTablesAsync(string connectionString, CancellationToken cancellationToken = default);

        /// <summary>
        /// Test connection to database
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="isPostgreSQL">Whether the connection is for PostgreSQL</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync(string connectionString, bool isPostgreSQL, CancellationToken cancellationToken = default);
    }