namespace Csag.WorkloadIdentity.Demo.Database
{
    public interface IAppDbContextFactory
    {
        Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
    }
}