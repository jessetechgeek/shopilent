using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Respawn;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Shopilent.API.IntegrationTests.Common;

public class ApiIntegrationTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSqlContainer { get; }
    public RedisContainer RedisContainer { get; }
    public MinioContainer MinioContainer { get; }
    public IContainer MeilisearchContainer { get; }
    public IContainer SeqContainer { get; }
    
    public IConfiguration Configuration { get; private set; } = null!;
    private Respawner _respawner = null!;

    public ApiIntegrationTestFixture()
    {
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:17.5")
            .WithDatabase("shopilent_api_integration_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCleanUp(true)
            .Build();

        RedisContainer = new RedisBuilder()
            .WithImage("redis:8.0.3-alpine")
            .WithCleanUp(true)
            .Build();

        MinioContainer = new MinioBuilder()
            .WithImage("minio/minio:RELEASE.2025-07-23T15-54-02Z-cpuv1")
            .WithUsername("minioadmin")
            .WithPassword("minioadmin123")
            .WithCleanUp(true)
            .Build();

        MeilisearchContainer = new ContainerBuilder()
            .WithImage("getmeili/meilisearch:v1.15.2")
            .WithPortBinding(7700, true)
            .WithEnvironment("MEILI_MASTER_KEY", "test-master-key")
            .WithEnvironment("MEILI_ENV", "development")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(7700).ForPath("/health")))
            .WithCleanUp(true)
            .Build();

        SeqContainer = new ContainerBuilder()
            .WithImage("datalust/seq:2025.2")
            .WithPortBinding(5341, 80)  // Host:5341 → Container:80 
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("SEQ_FIRSTRUN_ADMINPASSWORD", "Integration123!")
            .WithEnvironment("SEQ_FIRSTRUN_ADMINUSERNAME", "admin")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80)))  // Check container port 80
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        await PostgreSqlContainer.StartAsync();
        await RedisContainer.StartAsync();
        await MinioContainer.StartAsync();
        await MeilisearchContainer.StartAsync();
        await SeqContainer.StartAsync();

        await ConfigureSettings();
        await InitializeDatabase();
        await InitializeRespawner();
    }

    private Task ConfigureSettings()
    {
        var configurationBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = PostgreSqlContainer.GetConnectionString(),
                ["ConnectionStrings:PostgreSql"] = PostgreSqlContainer.GetConnectionString(),
                // Explicitly set empty read replicas array to force using write connection for reads
                ["ConnectionStrings:PostgreSqlReadReplicas:0"] = null,
                ["ConnectionStrings:PostgreSqlReadReplicas:1"] = null,
                ["Redis:ConnectionString"] = RedisContainer.GetConnectionString(),
                ["Redis:InstanceName"] = "ApiIntegrationTest",
                ["MinIO:Endpoint"] = MinioContainer.GetConnectionString(),
                ["MinIO:AccessKey"] = "minioadmin",
                ["MinIO:SecretKey"] = "minioadmin123",
                ["MinIO:BucketName"] = "test-bucket",
                ["MinIO:UseSSL"] = "false",
                ["S3:Provider"] = "MinIO",
                ["S3:AccessKey"] = "minioadmin",
                ["S3:SecretKey"] = "minioadmin123",
                ["S3:Region"] = "us-east-1",
                ["S3:DefaultBucket"] = "test-bucket",
                ["S3:ServiceUrl"] = MinioContainer.GetConnectionString(),
                ["S3:ForcePathStyle"] = "true",
                ["S3:UseSsl"] = "false",
                ["Jwt:Secret"] = "test-jwt-key-for-api-integration-tests-with-minimum-256-bits-length",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Jwt:TokenLifetimeMinutes"] = "30",
                ["Jwt:RefreshTokenLifetimeDays"] = "7",
                ["Email:SenderEmail"] = "test@shopilent.com",
                ["Email:SenderName"] = "Shopilent Test",
                ["Email:SmtpServer"] = "localhost",
                ["Email:SmtpPort"] = "587",
                ["Email:SmtpUsername"] = "testuser",
                ["Email:SmtpPassword"] = "testpass",
                ["Email:EnableSsl"] = "false",
                ["Email:SendEmails"] = "false",
                ["Email:AppUrl"] = "https://test.shopilent.com",
                ["Outbox:ProcessingIntervalSeconds"] = "30",
                ["Outbox:MaxRetryAttempts"] = "3",
                ["Outbox:BatchSize"] = "10",
                // MeiliSearch configuration for API integration tests
                ["Meilisearch:Url"] = $"http://localhost:{MeilisearchContainer.GetMappedPublicPort(7700)}",
                ["Meilisearch:ApiKey"] = "test-master-key",
                ["Meilisearch:Indexes:Products"] = "products_api_test",
                ["Meilisearch:BatchSize"] = "100",
                // Seq logging with TestContainer
                ["Seq:ServerUrl"] = $"http://localhost:{SeqContainer.GetMappedPublicPort(80)}",
                // Stripe test configuration
                ["Stripe:SecretKey"] = "sk_test_api_integration_test_key",
                ["Stripe:PublishableKey"] = "pk_test_api_integration_test_key",
                ["Stripe:WebhookSecret"] = "whsec_test_api_integration_webhook_secret",
                ["Stripe:ApiVersion"] = "2025-06-30",
                ["Stripe:EnableTestMode"] = "true",
                // Disable API rate limiting for integration tests to avoid flaky 429 responses.
                ["RateLimiting:Enabled"] = "false",
                // CORS configuration for API tests
                ["Cors:AllowedOrigins:0"] = "http://localhost:3000",
                ["Cors:AllowedOrigins:1"] = "http://localhost:5173"
            });

        Configuration = configurationBuilder.Build();
        return Task.CompletedTask;
    }

    private async Task InitializeDatabase()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Configuration);
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(PostgreSqlContainer.GetConnectionString());
        });

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await dbContext.Database.MigrateAsync();
    }

    private async Task InitializeRespawner()
    {
        using var connection = new NpgsqlConnection(PostgreSqlContainer.GetConnectionString());
        await connection.OpenAsync();
        
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            TablesToIgnore = ["__EFMigrationsHistory"],
            DbAdapter = DbAdapter.Postgres
        });
    }

    public async Task ResetDatabaseAsync()
    {
        using var connection = new NpgsqlConnection(PostgreSqlContainer.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        await PostgreSqlContainer.DisposeAsync();
        await RedisContainer.DisposeAsync();
        await MinioContainer.DisposeAsync();
        await MeilisearchContainer.DisposeAsync();
        await SeqContainer.DisposeAsync();
    }
}