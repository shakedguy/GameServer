using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Models;

namespace GameServer.Data;

public class PlayerStateRepository : IRepository<PlayerState>
{
    private readonly IDbClient _dbClient;
    private readonly ILogger<PlayerStateRepository> _logger;

    private const string TABLE_NAME = "player_states";

    public PlayerStateRepository(IDbClient dbClient,
        ILogger<PlayerStateRepository> logger)
    {
        _dbClient = dbClient;
        _logger = logger;
    }

    public async Task<PlayerState?> GetAsync(object id)
    {
        try
        {
            var sql = $"SELECT * FROM {TABLE_NAME} WHERE PlayerId = @id";
            return await _dbClient.QueryAsync(async connection =>
            {
                var player =
                    await connection.QueryFirstOrDefaultAsync<PlayerState>(
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

    public async Task<PlayerState?> GetByAsync(string field, object value)
    {
        try
        {
            var sql = $"SELECT * FROM {TABLE_NAME} WHERE {field} = @value";
            return await _dbClient.QueryAsync(async connection =>
            {
                var player =
                    await connection.QueryFirstOrDefaultAsync<PlayerState>(
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


    public async Task<PlayerState?> AddAsync(PlayerState entity)
    {
        try
        {
            var sql =
                $"INSERT INTO {TABLE_NAME} (PlayerId, Coins, Rolls) VALUES (@PlayerId, @Coins, @Rolls) RETURNING *";
            return await _dbClient.ExecuteAsync((connection, transaction) =>
            {
                return connection.QueryFirstOrDefaultAsync<PlayerState>(
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

    public async Task<PlayerState?> UpdateAsync(PlayerState entity)
    {
        try
        {
            var sql =
                $"UPDATE {TABLE_NAME} SET Coins=@Coins, Rolls=@Rolls WHERE PlayerId=@PlayerId RETURNING *";
            return await _dbClient.ExecuteAsync((connection, transaction) =>
            {
                return connection.QueryFirstOrDefaultAsync<PlayerState>(
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