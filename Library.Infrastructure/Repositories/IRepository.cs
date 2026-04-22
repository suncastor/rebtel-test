namespace Library.Infrastructure.Repositories;

public interface IRepository<T> where T : class
{
    IQueryable<T> GetAll();

    Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Delete(T entity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
