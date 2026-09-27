using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using BrightPath.Api.Endpoints;
using BrightPath.Api.Seed;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("BrightPath")
    ?? throw new InvalidOperationException(
        "Connection string 'BrightPath' is not set. See README for how to point it at your local PostgreSQL.");

var bookingPolicy = new BookingPolicy(
    builder.Configuration.GetSection(BookingPolicyOptions.Section).Get<BookingPolicyOptions>()
    ?? throw new InvalidOperationException($"Config section '{BookingPolicyOptions.Section}' is missing."));

builder.Services.AddSingleton(bookingPolicy);
builder.Services.AddDbContext<BrightPathDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<BrightPathDbContext>("database");
builder.Services.AddProblemDetails();

// Bad input that fails binding (e.g. ?from=not-a-date) is a 400, not a 500.
builder.Services.Configure<ExceptionHandlerOptions>(options =>
    options.StatusCodeSelector = ex => ex is BadHttpRequestException bad ? bad.StatusCode : StatusCodes.Status500InternalServerError);
builder.Services.AddOpenApi();

var app = builder.Build();

// Creates the database on first run, applies any pending migrations, then loads the exported week
// if the database is still empty.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BrightPathDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        await SeedLoader.SeedAsync(db, bookingPolicy, app.Logger);
    }
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapReportEndpoints();

app.Run();
