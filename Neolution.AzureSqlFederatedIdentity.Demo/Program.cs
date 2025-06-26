using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neolution.AzureSqlFederatedIdentity;
using Neolution.AzureSqlFederatedIdentity.Demo.Database;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

builder.Services.AddAzureSqlWorkloadIdentity(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Ok("Cloud Run to Azure SQL via Workload Identity Federation."));

app.MapGet("/test", async ([FromServices] IAppDbContextFactory dbFactory) =>
{
    try
    {
        await using var context = await dbFactory.CreateDbContextAsync();

        var count = await context.TestTable.CountAsync();
        if (count == 0)
        {
            return Results.NotFound("No rows found in TestTable.");
        }

        var rows = await context.TestTable.OrderBy(e => e.Id).ToListAsync();
        return Results.Ok(new { count, rows, });
    }
    catch (Exception ex)
    {
        // In production, avoid exposing details; here for debugging:
        return Results.Problem(detail: ex.Message);
    }
});

app.Run();
