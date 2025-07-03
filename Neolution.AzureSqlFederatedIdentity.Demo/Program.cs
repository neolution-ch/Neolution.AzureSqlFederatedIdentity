using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neolution.AzureSqlFederatedIdentity;
using Neolution.AzureSqlFederatedIdentity.Demo.Database;
using Neolution.AzureSqlFederatedIdentity.Demo.Extensions;
using Neolution.AzureSqlFederatedIdentity.Demo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureSqlWorkloadIdentity(builder.Configuration);

builder.Services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

builder.Services.AddBlobStorageClient(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Ok("Cloud Run to Azure SQL via Workload Identity Federation."));

app.MapGet("/test", async ([FromServices] IAppDbContextFactory dbFactory, [FromServices] IBlobStorageService blobService, CancellationToken cancellationToken) =>
{
    try
    {
        await using var context = await dbFactory.CreateDbContextAsync(cancellationToken);

        var count = await context.TestTable.CountAsync(cancellationToken);
        if (count == 0)
        {
            return Results.NotFound("No rows found in TestTable.");
        }

        var rows = await context.TestTable.OrderBy(e => e.Id).ToListAsync(cancellationToken);

        var blobContent = await blobService.DownloadTestFileAsync(cancellationToken);

        return Results.Ok(new { count, rows, blobContent });
    }
    catch (Exception ex)
    {
        // In production, avoid exposing details; here for debugging:
        return Results.Problem(detail: ex.Message);
    }
});

app.Run();
