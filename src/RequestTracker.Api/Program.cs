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
    .AddJsonOptions(options =>
        // Numbers are rejected so a value like 99 can't bind to an undefined enum member.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
// The OpenAPI document reads these options, not the MVC ones above; without this it describes enums as integers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

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
