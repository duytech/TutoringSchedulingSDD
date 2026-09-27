using BrightPath.Api.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BrightPath")
    ?? throw new InvalidOperationException(
        "Connection string 'BrightPath' is not set. See README for how to point it at your local PostgreSQL.");

builder.Services.AddDbContext<BrightPathDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<BrightPathDbContext>("database");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Creates the database on first run and applies any pending migrations.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<BrightPathDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");

app.Run();
