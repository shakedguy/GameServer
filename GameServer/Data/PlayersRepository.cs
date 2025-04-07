using System.Data;
using Dapper;
using GameServer.Data;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace GameServer.Data;

public class PlayersRepository : IRepository<Player>
{
    private readonly IDbClient _dbClient;
    private readonly ILogger<PlayersRepository> _logger;

    private const string TABLE_NAME = "players";

    public PlayersRepository(IDbClient dbClient,
        ILogger<PlayersRepository> logger)
    {
        _dbClient = dbClient;
        _logger = logger;
    }

    public async Task<Player?> GetAsync(object id)
    {
        try
        {
            var sql = $"SELECT * FROM {TABLE_NAME} WHERE id = @id";
            return await _dbClient.QueryAsync<Player>(async connection =>
            {
                var player = await connection.QueryFirstOrDefaultAsync<Player>(
                    sql, new { id });
                return player;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return null;
        }
    }

    public async Task<Player?> GetByAsync(string field, object value)
    {
        try
        {
            var sql = $"SELECT * FROM {TABLE_NAME} WHERE {field} = @value";
            return await _dbClient.QueryAsync<Player>(async connection =>
            {
                var player = await connection.QueryFirstOrDefaultAsync<Player>(
                    sql, new { value });
                return player;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return null;
        }
    }

    public async Task<Player?> AddAsync(Player entity)
    {
        try
        {
            var sql =
                $"INSERT OR IGNORE INTO {TABLE_NAME} (Id, DeviceId) VALUES (@Id, @DeviceId) RETURNING *";
            return await _dbClient.ExecuteAsync((connection, transaction) =>
            {
                return connection.QueryFirstOrDefaultAsync<Player>(
                    sql,
                    entity,
                    transaction: transaction);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return null;
        }
    }

    public async Task<Player?> UpdateAsync(Player entity)
    {
        try
        {
            var sql =
                $"UPDATE {TABLE_NAME} SET Id=@Id, DeviceId=@DeviceId WHERE Id = @Id RETURNING *";
            return await _dbClient.ExecuteAsync((connection, transaction) =>
            {
                return connection.QueryFirstOrDefaultAsync<Player>(
                    sql,
                    entity,
                    transaction: transaction);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return null;
        }
    }
}