namespace Csag.WorkloadIdentity.Demo.Services
{
    public interface IBlobStorageService
    {
        Task<string> DownloadTestFileAsync(CancellationToken cancellationToken);
    }
}
