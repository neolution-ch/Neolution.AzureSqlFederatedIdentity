using Microsoft.AspNetCore.Mvc;
using Csag.WorkloadIdentity;
using Csag.WorkloadIdentity.Demo.Database;
using Csag.WorkloadIdentity.Demo.Extensions;
using Csag.WorkloadIdentity.Demo.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IAppDbContextFactory, AppDbContextFactory>();

builder.Services.AddWorkloadIdentity(builder.Configuration);
builder.Services.AddBlobStorageClient(builder.Configuration);

var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Ok("Azure SQL and Blob Storage through Csag.WorkloadIdentity, without stored credentials."));

app.MapGet("/test", async ([FromServices] IAppDbContextFactory dbFactory, [FromServices] IBlobStorageService? blobService, [FromServices] ILogger<Program> logger, CancellationToken cancellationToken) =>
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

        // The Blob Storage service exists only when the BlobStorage resource and a test file are configured.
        var blobContent = blobService is null ? null : await blobService.DownloadTestFileAsync(cancellationToken);

        return Results.Ok(new { count, rows, blobContent, blobSkipped = blobService is null, });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        // The client went away; there is nobody to answer and nothing worth logging.
        throw;
    }
    catch (Exception ex)
    {
        // The endpoint is unauthenticated, so token, SQL and Blob Storage failures go to the log, not the response.
        logger.LogError(ex, "Reading TestTable or downloading the test blob failed.");
        return Results.Problem();
    }
});

app.Run();
