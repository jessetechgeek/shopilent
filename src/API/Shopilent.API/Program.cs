using System.Text.Json;
using FastEndpoints;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Shopilent.API.Common.Services;
using Shopilent.Application.Extensions;
using Shopilent.Infrastructure.Cache.Redis.Extensions;
using Shopilent.Infrastructure.Extensions;
using Shopilent.Infrastructure.Identity.Extensions;
using Shopilent.Infrastructure.Logging.Configuration;
using Shopilent.Infrastructure.Payments.Extensions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Configuration.Extensions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Extensions;
using Shopilent.Infrastructure.S3ObjectStorage.Extensions;
using Shopilent.Infrastructure.Search.Meilisearch.Extensions;

var builder = WebApplication.CreateBuilder(args);

LoggingConfiguration.ConfigureLogging(builder);

builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);
builder.Services.AddPostgresPersistence(builder.Configuration);
builder.Services.AddCacheServices(builder.Configuration);
builder.Services.AddStorageServices(builder.Configuration);
builder.Services.AddPaymentServices(builder.Configuration);
builder.Services.AddMeilisearch(builder.Configuration);

builder.Services.AddSingleton<IFilterEncodingService, FilterEncodingService>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Shopilent.API.Program).Assembly);
});

builder.Services.AddFastEndpoints();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Content-Disposition")
            .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/openapi/{documentName}.json");
    app.MapScalarApiReference(options =>
        {
            options
                .WithTitle("Shopilent API")
                .WithTheme(ScalarTheme.Saturn)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.AddServer("http://localhost:9801");
            options.AddServer("http://localhost:5004");
        }
    );
}

app.UseHttpsRedirection();

app.MapHealthChecks("health", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse });

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseFastEndpoints(config =>
    {
        config.Endpoints.RoutePrefix = "api";
        config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }
);

app.Run();

namespace Shopilent.API
{
    public partial class Program;
}
