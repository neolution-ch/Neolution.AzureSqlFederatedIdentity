namespace Csag.AzureSqlFederatedIdentity.Demo.Database
{
    public interface IAppDbContextFactory
    {
        Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
    }
}