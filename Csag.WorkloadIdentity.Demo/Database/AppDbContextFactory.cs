namespace Csag.WorkloadIdentity.Demo.Database
{
    using System;
    using Csag.WorkloadIdentity.Abstractions;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;

    public class AppDbContextFactory : IAppDbContextFactory
    {
        private readonly string? connectionString;
        private readonly IAzureSqlTokenProvider tokenProvider;

        public AppDbContextFactory(IConfiguration configuration, IAzureSqlTokenProvider tokenProvider)
        {
            this.connectionString = configuration.GetConnectionString("DefaultConnection");
            this.tokenProvider = tokenProvider;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            // Checked on use: the factory itself is constructed while the endpoint's parameters are bound, which is
            // outside the handler's error handling.
            if (string.IsNullOrWhiteSpace(this.connectionString))
            {
                throw new InvalidOperationException("Connection string 'DefaultConnection' not set.");
            }

            // The connection string carries no credentials; the federated access token authenticates the connection.
            var accessToken = await this.tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);

            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(this.connectionString).Options;
            var context = new AppDbContext(options);
            if (context.Database.GetDbConnection() is SqlConnection sqlConnection)
            {
                sqlConnection.AccessToken = accessToken;
            }

            return context;
        }
    }
}
