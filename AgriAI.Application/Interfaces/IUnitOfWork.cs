namespace AgriAI.Application.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity> Repository<TEntity>() where TEntity : Domain.Common.BaseEntity;
        Task<int> CompleteAsync();
    }
}
