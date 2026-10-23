using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Abstractions;
using Csag.WorkloadIdentity.Demo.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);

builder.Services.AddPooledDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// The endpoint is unauthenticated, so the exception handler logs token-exchange and SQL failures and answers with a
// generic problem response.
builder.Services.AddProblemDetails();

var app = builder.Build();

// Before anything opens a SQL connection.
if (!SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDefault,
        new FederatedSqlAuthenticationProvider(app.Services.GetRequiredService<IAzureSqlTokenProvider>())))
{
    throw new InvalidOperationException("Could not install the federated SQL authentication provider.");
}

app.UseExceptionHandler();
app.UseStaticFiles();

app.MapGet("/", () => Results.Ok("Cloud Run to Azure SQL via Workload Identity Federation."));

app.MapGet("/test", async (AppDbContext context, CancellationToken cancellationToken) =>
{
    var count = await context.TestTable.CountAsync(cancellationToken);
    if (count == 0)
    {
        return Results.NotFound("No rows found in TestTable.");
    }

    var rows = await context.TestTable.OrderBy(e => e.Id).ToListAsync(cancellationToken);
    return Results.Ok(new { count, rows, });
});

app.Run();
