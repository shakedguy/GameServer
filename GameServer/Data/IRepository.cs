namespace GameServer.Data;

public interface IRepository<T>
    where T : class
{
    Task<T?> GetAsync(object id);
    
    Task<T?> GetByAsync(string field, object value);


    Task<T?> AddAsync(T entity);


    Task<T?> UpdateAsync(T entity);
}