namespace Csag.AzureSqlFederatedIdentity.Demo.Database
{
    using System;
    using Csag.AzureSqlFederatedIdentity.Abstractions;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContextFactory : IAppDbContextFactory
    {
        private readonly DbContextOptions<AppDbContext> options;
        private readonly IAzureSqlTokenProvider tokenProvider;

        public AppDbContextFactory(IConfiguration configuration, IAzureSqlTokenProvider tokenProvider)
        {
            this.tokenProvider = tokenProvider;

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not set.");
            }

            this.options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            // The connection string carries no credentials; the federated access token authenticates the connection.
            var accessToken = await this.tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);

            var context = new AppDbContext(this.options);
            if (context.Database.GetDbConnection() is SqlConnection sqlConnection)
            {
                sqlConnection.AccessToken = accessToken;
            }

            return context;
        }
    }
}
