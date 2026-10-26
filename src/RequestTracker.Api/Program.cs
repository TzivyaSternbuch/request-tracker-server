using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using RequestTracker.Api.ErrorHandling;
using RequestTracker.Core;
using RequestTracker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCore();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddControllers(options =>
        // Model-binding errors use the JSON (camelCase) property names, matching the request body.
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create);
// The OpenAPI document reads these options, not the MVC ones above; without them it describes enums as integers.
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

// Disposed on exit, which flushes the console logger, so the seed command's last log line isn't lost.
await using var app = builder.Build();

// `dotnet run --project src/RequestTracker.Api -- seed`: regenerate the test data, then exit without serving requests.
if (args.Contains("seed"))
{
    // The seeder drops the database, so a misconfigured environment must not be able to point it at real data.
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("The seed command recreates the database and runs only in Development.");
    }

    await app.Services.SeedDatabaseAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    // Creates the database if needed and applies pending migrations, so running locally needs no manual step.
    await app.Services.MigrateDatabaseAsync();

    if (app.Configuration.GetValue<bool>("Seeding:SeedOnStartup"))
    {
        await app.Services.SeedDatabaseIfEmptyAsync();
    }
}

app.UseExceptionHandler();
// Errors without an exception (unknown route, wrong method) get a ProblemDetails body too.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.MapControllers();

app.Run();

// One JSON configuration for both option sets, so the API and its OpenAPI document can't drift apart.
static void ConfigureJson(JsonSerializerOptions options) =>
    // Numbers are rejected so a value like 99 can't bind to an undefined enum member.
    options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
