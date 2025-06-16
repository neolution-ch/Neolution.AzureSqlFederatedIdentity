namespace Neolution.AzureSqlFederatedIdentity.Demo.Database
{
    using System;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;

    public class AppDbContextFactory : IAppDbContextFactory, IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptionsBuilder<AppDbContext> optionsBuilder;
        private readonly IAzureSqlTokenProvider tokenProvider;

        public AppDbContextFactory(IConfiguration configuration, IAzureSqlTokenProvider tokenProvider)
        {
            this.tokenProvider = tokenProvider;
            this.optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not set.");
            this.optionsBuilder.UseSqlServer(connectionString);
        }

        public AppDbContext CreateDbContext()
        {
            return this.CreateDbContextAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var context = new AppDbContext(this.optionsBuilder.Options);
            if (context.Database.GetDbConnection() is SqlConnection sqlConnection)
            {
                sqlConnection.AccessToken = await this.tokenProvider.GetAzureSqlAccessTokenAsync(cancellationToken);
            }

            return context;
        }
    }
}
