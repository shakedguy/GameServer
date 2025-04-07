using System.Data;
using System.Data.Common;
using Dapper;
using GameServer.Configs;
using Microsoft.Extensions.Options;

namespace GameServer.Data;

public interface IDbClient
{
    Task<T?> QueryAsync<T>(Func<IDbConnection, Task<T?>> action,
        CancellationToken cancellationToken = default);

    Task<T?> ExecuteAsync<T>(
        Func<IDbConnection, DbTransaction, Task<T?>> action,
        CancellationToken cancellationToken = default);

    T? Execute<T>(Func<IDbConnection, DbTransaction, T> action);
    void Execute(Action<IDbConnection, DbTransaction> action);

    void Initialize();
}

public class DbClient : IDbClient
{
    private readonly DbDataSource _connectionFactory;

    private readonly DbSettings _settings;

    private static readonly string _dirPath =
        Directory.Exists(Path.Combine(Environment.CurrentDirectory,
            "GameServer"))
            ? Path.Combine(Environment.CurrentDirectory, "GameServer")
            : Environment.CurrentDirectory;


    public DbClient(DbDataSource connectionFactory,
        IOptions<DbSettings> options)
    {
        _connectionFactory = connectionFactory;
        _settings = options.Value;
    }

    public void Initialize()
    {
        using var connection =
            _connectionFactory.OpenConnection();
        ValidateMigrationTable(connection);

        Migrate(connection);
    }

    public async Task<T?> QueryAsync<T>(Func<IDbConnection, Task<T?>> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Invalid connection string");
        }


        return await action(connection);
    }

    public async Task<T?> ExecuteAsync<T>(
        Func<IDbConnection, DbTransaction, Task<T?>> action,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            await _connectionFactory.OpenConnectionAsync(cancellationToken);
        if (connection is null)
        {
            throw new InvalidOperationException("Invalid connection string");
        }

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await action(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception("Error executing query", ex);
        }
    }

    public T? Execute<T>(Func<IDbConnection, DbTransaction, T> action)
    {
        using var connection = _connectionFactory.OpenConnection();
        if (connection is null)
        {
            throw new InvalidOperationException("Invalid connection string");
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            var result = action(connection, transaction);
            transaction.Commit();
            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new Exception("Error executing query", ex);
        }
    }

    public void Execute(Action<IDbConnection, DbTransaction> action)
    {
        using var connection = _connectionFactory.OpenConnection();
        if (connection is null)
        {
            throw new InvalidOperationException("Invalid connection string");
        }

        using var transaction = connection.BeginTransaction();
        try
        {
            action(connection, transaction);
            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            throw new Exception("Error executing query", ex);
        }
    }


    private void ValidateMigrationTable(DbConnection connection)
    {
        var createMigrationTableSql =
            "CREATE TABLE IF NOT EXISTS migrations (id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT NOT NULL UNIQUE);";
        connection.Execute(createMigrationTableSql);
    }

    public void Migrate(DbConnection connection
    )
    {
        var migrationPath =
            Path.GetFullPath(Path.Combine(_dirPath, _settings.MigrationsPath));
        if (!Directory.Exists(migrationPath))
        {
            throw new DirectoryNotFoundException(
                $"Migrations directory \"{migrationPath}\" does not exist.");
        }

        foreach (var migrationFile in Directory.EnumerateFiles(migrationPath)
                     .Order())
        {
            var migrationName = Path.GetFileName(migrationFile);
            var migrationExists = connection.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM migrations WHERE name = @Name",
                new { Name = migrationName });
            if (migrationExists == 0)
            {
                var sql = File.ReadAllText(migrationFile);
                connection.ExecuteAsync(sql);
                connection.Execute(
                    "INSERT INTO migrations (name) VALUES (@Name)",
                    new { Name = migrationName });
            }
        }
    }
}