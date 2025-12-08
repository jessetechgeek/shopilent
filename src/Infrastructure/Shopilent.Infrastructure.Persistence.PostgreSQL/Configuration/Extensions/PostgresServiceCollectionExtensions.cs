using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Dapper.TypeHandlers;
using Shopilent.Application.Abstractions.Persistence;
using Shopilent.Domain.Audit.Repositories.Read;
using Shopilent.Domain.Audit.Repositories.Write;
using Shopilent.Domain.Catalog.Repositories.Read;
using Shopilent.Domain.Catalog.Repositories.Write;
using Shopilent.Domain.Identity.Repositories.Read;
using Shopilent.Domain.Identity.Repositories.Write;
using Shopilent.Domain.Outbox.Repositories.Read;
using Shopilent.Domain.Outbox.Repositories.Write;
using Shopilent.Domain.Payments.Repositories.Read;
using Shopilent.Domain.Payments.Repositories.Write;
using Shopilent.Domain.Sales.Repositories.Read;
using Shopilent.Domain.Sales.Repositories.Write;
using Shopilent.Domain.Shipping.Repositories.Read;
using Shopilent.Domain.Shipping.Repositories.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Abstractions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Context;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Extensions;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Factories;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Interceptors;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Audit.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Audit.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Catalog.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Identity.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Identity.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Outbox.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Outbox.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Payments.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Sales.Write;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Shipping.Read;
using Shopilent.Infrastructure.Persistence.PostgreSQL.Repositories.Shipping.Write;

namespace Shopilent.Infrastructure.Persistence.PostgreSQL.Configuration.Extensions;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var connectionConfig = new PostgresConnectionConfig
        {
            WriteConnectionString = configuration.GetConnectionString("PostgreSql") ?? string.Empty,
            ReadConnectionStrings = configuration.GetSection("ConnectionStrings:PostgreSqlReadReplicas")
                .Get<List<string>>() ?? new List<string>()
        };
        services.AddSingleton(connectionConfig);

        // Register Dapper type handlers for automatic JSONB conversion
        SqlMapper.AddTypeHandler(new JsonDictionaryTypeHandler());

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();

            options.UseNpgsql(
                connectionConfig.WriteConnectionString,
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));

            options.AddInterceptors(interceptor);
        });

        services.AddScoped<IDbConnection>(provider =>
        {
            var config = provider.GetRequiredService<PostgresConnectionConfig>();
            var connection = new NpgsqlConnection(config.GetReadConnectionString());
            connection.Open();
            return connection;
        });

        services.AddScoped<IDapperConnectionFactory, DapperConnectionFactory>();

        AddWriteRepositories(services);
        AddReadRepositories(services);

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHealthChecks(configuration);

        return services;
    }

    private static void AddWriteRepositories(IServiceCollection services)
    {
        // Identity repositories
        services.AddScoped<IUserWriteRepository, UserWriteRepository>();
        services.AddScoped<IRefreshTokenWriteRepository, RefreshTokenWriteRepository>();

        //Catalog repositories
        services.AddScoped<ICategoryWriteRepository, CategoryWriteRepository>();
        services.AddScoped<IProductWriteRepository, ProductWriteRepository>();
        services.AddScoped<IAttributeWriteRepository, AttributeWriteRepository>();
        services.AddScoped<IProductVariantWriteRepository, ProductVariantWriteRepository>();

        // Sales repositories
        services.AddScoped<ICartWriteRepository, CartWriteRepository>();
        services.AddScoped<IOrderWriteRepository, OrderWriteRepository>();

        // Payment repositories
        services.AddScoped<IPaymentWriteRepository, PaymentWriteRepository>();
        services.AddScoped<IPaymentMethodWriteRepository, PaymentMethodWriteRepository>();

        //Shipping repositories
        services.AddScoped<IAddressWriteRepository, AddressWriteRepository>();

        // Audit repositories
        services.AddScoped<IAuditLogWriteRepository, AuditLogWriteRepository>();

        services.AddScoped<IOutboxMessageWriteRepository, OutboxMessageWriteRepository>();
    }

    private static void AddReadRepositories(IServiceCollection services)
    {
        // Identity repositories
        services.AddScoped<IUserReadRepository, UserReadRepository>();
        services.AddScoped<IRefreshTokenReadRepository, RefreshTokenReadRepository>();

        //Catalog repositories
        services.AddScoped<ICategoryReadRepository, CategoryReadRepository>();
        services.AddScoped<IProductReadRepository, ProductReadRepository>();
        services.AddScoped<IAttributeReadRepository, AttributeReadRepository>();
        services.AddScoped<IProductVariantReadRepository, ProductVariantReadRepository>();

        // Sales repositories
        services.AddScoped<ICartReadRepository, CartReadRepository>();
        services.AddScoped<IOrderReadRepository, OrderReadRepository>();

        // Payment repositories
        services.AddScoped<IPaymentReadRepository, PaymentReadRepository>();
        services.AddScoped<IPaymentMethodReadRepository, PaymentMethodReadRepository>();

        //Shipping repositories
        services.AddScoped<IAddressReadRepository, AddressReadRepository>();

        // Audit repositories
        services.AddScoped<IAuditLogReadRepository, AuditLogReadRepository>();

        // Outbox repositories
        services.AddScoped<IOutboxMessageReadRepository, OutboxMessageReadRepository>();
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionConfig = new PostgresConnectionConfig
        {
            WriteConnectionString = configuration.GetConnectionString("PostgreSql") ?? string.Empty,
            ReadConnectionStrings = configuration.GetSection("ConnectionStrings:PostgreSqlReadReplicas")
                .Get<List<string>>() ?? new List<string>()
        };

        var healthChecks = services.AddHealthChecks();

        // Add main write database health check
        healthChecks.AddNpgSql(connectionConfig.WriteConnectionString, name: "postgresql-main");

        // Add health checks for each read replica
        for (int i = 0; i < connectionConfig.ReadConnectionStrings.Count; i++)
        {
            var replicaConnectionString = connectionConfig.ReadConnectionStrings[i];
            if (!string.IsNullOrEmpty(replicaConnectionString))
            {
                healthChecks.AddNpgSql(replicaConnectionString, name: $"postgresql-replica-{i + 1}");
            }
        }

        return services;
    }
}
