using MassTransit;
using CI.Kernel;
using CI.Kernel.InMemory;
using CI.Kernel.Redis;
using CI.Connector.OpenBanking.Core;
using CI.Connector.OpenBanking.Core.Commands;
using CI.Connector.OpenBanking.Core.DTOs;
using CI.Connector.OpenBanking.Core.Handlers;
using CI.Connector.OpenBanking.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CI.Connector.OpenBanking.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddOpenBankingServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<OpenBankingDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("OpenBanking")));

        services.AddScoped<IOpenBankingRepository, OpenBankingRepository>();
        services.AddScoped<IOpenBankingOutbox, OpenBankingOutbox>();

        services.AddScoped<ICommandHandler<ConnectBankCommand, Guid>, ConnectBankHandler>();
        services.AddScoped<ICommandHandler<DisconnectBankCommand>, DisconnectBankHandler>();
        services.AddScoped<ICommandHandler<RefreshConnectionCommand>, RefreshConnectionHandler>();
        services.AddScoped<ICommandHandler<ImportStatementCommand, Guid>, ImportStatementHandler>();
        services.AddScoped<ICommandHandler<MatchStatementLineCommand>, MatchStatementLineHandler>();
        services.AddScoped<ICommandHandler<IgnoreStatementLineCommand>, IgnoreStatementLineHandler>();
        services.AddScoped<ICommandHandler<GetConnectionQuery, ConnectionDto>, GetConnectionHandler>();
        services.AddScoped<ICommandHandler<ListConnectionsQuery, PagedResult<ConnectionSummaryDto>>, ListConnectionsHandler>();
        services.AddScoped<ICommandHandler<GetStatementQuery, StatementDto>, GetStatementHandler>();
        services.AddScoped<ICommandHandler<ListStatementsQuery, PagedResult<StatementSummaryDto>>, ListStatementsHandler>();
        services.AddScoped<ICommandBus, HandlerDispatcher>();

        services.AddSingleton<IModuleManifest, OpenBankingModuleManifest>();

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
            services.AddRedisKernel(redis);
        else
            services.AddSingleton<IDistributedLock, NullDistributedLock>();

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = config["Keycloak:Authority"];
                opts.Audience  = config["Keycloak:Audience"] ?? "account";
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = !string.IsNullOrEmpty(config["Keycloak:Authority"]),
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                };
                opts.RequireHttpsMetadata = false;
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ci-connector-openbanking"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation()
                 .AddEntityFrameworkCoreInstrumentation();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });
        return services;
    }

    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration config)
    {
        var rabbitHost = config["RabbitMQ:Host"];
        if (string.IsNullOrEmpty(rabbitHost))
            return services;

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "ci");
                    h.Password(config["RabbitMQ:Password"] ?? "ci");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });
        services.AddHostedService<OutboxPublisher>();
        return services;
    }
}
