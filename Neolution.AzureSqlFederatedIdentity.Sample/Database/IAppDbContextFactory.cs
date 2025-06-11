namespace Neolution.AzureSqlFederatedIdentity.Sample.Database
{
    public interface IAppDbContextFactory
    {
        Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
    }
}