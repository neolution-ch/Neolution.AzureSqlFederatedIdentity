using Microsoft.AspNetCore.Mvc;
using Csag.AzureSqlFederatedIdentity;
using Csag.AzureSqlFederatedIdentity.Demo.Database;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

builder.Services.AddAzureSqlFederatedIdentity(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Ok("Cloud Run to Azure SQL via Workload Identity Federation."));

app.MapGet("/test", async ([FromServices] IAppDbContextFactory dbFactory, [FromServices] ILogger<Program> logger, CancellationToken cancellationToken) =>
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
        return Results.Ok(new { count, rows, });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // The client went away; there is nobody to answer and nothing worth logging.
        throw;
    }
    catch (Exception ex)
    {
        // The endpoint is unauthenticated, so token-exchange and SQL failures go to the log, not the response.
        logger.LogError(ex, "Querying TestTable failed.");
        return Results.Problem();
    }
});

app.Run();
