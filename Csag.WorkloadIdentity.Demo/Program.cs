using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Abstractions;
using Csag.WorkloadIdentity.Demo.Database;
using Csag.WorkloadIdentity.Demo.Extensions;
using Csag.WorkloadIdentity.Demo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkloadIdentity(builder.Configuration);
builder.Services.AddBlobStorageClient(builder.Configuration);

builder.Services.AddPooledDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(serviceProvider => serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// The endpoint is unauthenticated, so the exception handler logs token, SQL and Blob Storage failures and answers
// with a generic problem response.
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

app.MapGet("/", () => Results.Ok("Azure SQL and Blob Storage through Csag.WorkloadIdentity, without stored credentials."));

// The Blob Storage service is registered only when the BlobStorage resource and a test file are configured. Minimal
// APIs infer only registered services, so the optional one is marked [FromServices] and arrives as null otherwise.
app.MapGet("/test", async (AppDbContext context, [FromServices] IBlobStorageService? blobService, CancellationToken cancellationToken) =>
{
    var count = await context.TestTable.CountAsync(cancellationToken);
    if (count == 0)
    {
        return Results.NotFound("No rows found in TestTable.");
    }

    var rows = await context.TestTable.OrderBy(e => e.Id).ToListAsync(cancellationToken);
    var blobContent = blobService is null ? null : await blobService.DownloadTestFileAsync(cancellationToken);

    return Results.Ok(new { count, rows, blobContent, blobSkipped = blobService is null, });
});

app.Run();
