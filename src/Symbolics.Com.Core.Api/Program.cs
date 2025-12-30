using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Polly;
using Serilog;
using Symbolics.Com.Core.Api.Settings;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Application.Services;
using Symbolics.Com.Core.Application.Workers;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;
using Symbolics.Com.Core.Infrastructure.ExternalServices;
using Symbolics.Com.Core.Infrastructure.HealthChecks;
using Symbolics.Com.Core.Infrastructure.Logging;
using Symbolics.Com.Core.Infrastructure.Persistence;
using Symbolics.Com.Core.Infrastructure.Qdrant;
using Symbolics.Com.Core.Infrastructure.Repositories;
using Symbolics.Com.Core.Infrastructure.Services;
using Symbolics.Com.Core.Infrastructure.Workers;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

var connectionString = builder.Configuration.GetConnectionString("CoreDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'CoreDatabase' is missing from configuration.");
}

builder.Services.AddDbContext<CoreDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.AddScoped<IStreamRepository, StreamRepository>();
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));
builder.Services.Configure<ServiceSettings>(builder.Configuration.GetSection("Service"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.Configure<LogOptions>(builder.Configuration.GetSection("Log"));
builder.Services.Configure<TwitchOptions>(builder.Configuration.GetSection("Twitch"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.Configure<TwitchDiscoveryOptions>(builder.Configuration.GetSection("TwitchDiscovery"));
builder.Services.Configure<TwitchEnrichmentOptions>(builder.Configuration.GetSection("TwitchEnrichment"));
builder.Services.Configure<IgdbRefreshOptions>(builder.Configuration.GetSection("IgdbRefresh"));
builder.Services.Configure<StreamMaintenanceOptions>(builder.Configuration.GetSection("StreamMaintenance"));
builder.Services.Configure<SwaggerSettings>(builder.Configuration.GetSection("Swagger"));
builder.Services.Configure<RecommendationOptions>(builder.Configuration.GetSection("Recommendations"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient<QdrantClient>();
builder.Services.AddSingleton<IQdrantClient>(sp => sp.GetRequiredService<QdrantClient>());
builder.Services.AddHttpClient("TwitchAuth");
builder.Services.AddTransient<TwitchTokenHandler>();
builder.Services.AddHttpClient<ITwitchService, TwitchService>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptionsMonitor<TwitchOptions>>().CurrentValue;
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            var baseUrl = options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
        }
    })
    .AddHttpMessageHandler<TwitchTokenHandler>()
    .AddStandardResilienceHandler(options => ConfigureHttpResilience(options, "Twitch", TimeSpan.FromSeconds(15)));
builder.Services.AddHttpClient<IAiService, GeminiAiService>()
    .AddStandardResilienceHandler(options => ConfigureHttpResilience(options, "Gemini", TimeSpan.FromSeconds(30)));
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IConsumptionTracker, ConsumptionTracker>();
builder.Services.AddScoped<IWorkerRepository, WorkerRepository>();
builder.Services.AddScoped<IStreamMaintenanceService, StreamMaintenanceService>();
builder.Services.AddScoped<IStreamerLanguageService, StreamerLanguageService>();
builder.Services.AddScoped<ICampaignVectorizationService, CampaignVectorizationService>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<IStreamerRepository, StreamerRepository>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
builder.Services.AddSingleton<IRecommendationQueue, RecommendationQueue>();
builder.Services.AddHostedService(sp => {
    var scope = sp.CreateScope();
    return new TwitchDiscoveryWorker(
        scope.ServiceProvider.GetRequiredService<ITwitchService>(),
        scope.ServiceProvider.GetRequiredService<IWorkerRepository>(),
        scope.ServiceProvider.GetRequiredService<IStreamMaintenanceService>(),
        scope.ServiceProvider.GetRequiredService<IStreamerLanguageService>(),
        sp.GetRequiredService<IOptionsMonitor<TwitchDiscoveryOptions>>(),
        sp.GetRequiredService<ILogger<TwitchDiscoveryWorker>>()
    );
});
builder.Services.AddHostedService(sp =>
{
    return new TwitchEnrichmentWorker(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IOptionsMonitor<TwitchEnrichmentOptions>>(),
        sp.GetRequiredService<ILogger<TwitchEnrichmentWorker>>()
    );
});
builder.Services.AddHostedService(sp =>
{
    return new IgdbRefreshWorker(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<IOptionsMonitor<IgdbRefreshOptions>>(),
        sp.GetRequiredService<ILogger<IgdbRefreshWorker>>()
    );
});
builder.Services.AddHostedService<RecommendationWorker>();
builder.Services.AddHostedService<QdrantCollectionInitializer>();
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString,
        name: "PostgreSQL",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["db", "sql"])
    .AddCheck<QdrantHealthCheck>("Qdrant", failureStatus: HealthStatus.Unhealthy)
    .AddCheck<SeqHealthCheck>("Seq", failureStatus: HealthStatus.Degraded);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Symbolics.Com.Core.Api",
        Version = "v1"
    });

    options.AddSecurityDefinition("AdminKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Admin-Key",
        Description = "Admin key header for protected endpoints."
    });

    options.AddSecurityDefinition("ServiceKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Service-Key",
        Description = "Service key header for protected endpoints."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AdminKey"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ServiceKey"
                }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

var app = builder.Build();

// Enable Swagger for all environments with authentication


// Add basic authentication middleware for Swagger
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/swagger"))
    {
        var swaggerSettings = context.RequestServices.GetRequiredService<IOptions<SwaggerSettings>>().Value;
        
        if (!string.IsNullOrWhiteSpace(swaggerSettings.Username) && !string.IsNullOrWhiteSpace(swaggerSettings.Password))
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Basic "))
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"Swagger\"";
                context.Response.StatusCode = 401;
                return;
            }

            var token = authHeader.Substring("Basic ".Length).Trim();
            var credentialBytes = Convert.FromBase64String(token);
            var credentials = System.Text.Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            
            if (credentials.Length != 2 || 
                credentials[0] != swaggerSettings.Username || 
                credentials[1] != swaggerSettings.Password)
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
    }
    
    await next();
});

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    var swaggerSettings = app.Services.GetRequiredService<IOptions<SwaggerSettings>>().Value;
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Symbolics.Com.Core API";
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    if (dbContext.Database.GetPendingMigrations().Any())
    {
        dbContext.Database.Migrate();
    }
}

app.MapControllers();
app.MapGet("/api/system/version", (IConfiguration config) =>
{
    return Results.Ok(new
    {
        Version = config["APP_VERSION"] ?? "Unknown",
        Environment = config["ASPNETCORE_ENVIRONMENT"]
    });
}).WithTags("System");
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                component = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                duration = entry.Value.Duration
            })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}).WithTags("Health");

app.Run();

static void ConfigureHttpResilience(
    HttpStandardResilienceOptions options,
    string clientName,
    TimeSpan totalTimeout)
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.ShouldHandle = args =>
    {
        var isTransient = HttpClientResiliencePredicates.IsTransient(args.Outcome);
        var isRateLimited = args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests;
        return ValueTask.FromResult(isTransient || isRateLimited);
    };
    options.Retry.DelayGenerator = static args =>
    {
        var retryAfter = args.Outcome.Result?.Headers.RetryAfter?.Delta;
        return ValueTask.FromResult<TimeSpan?>(retryAfter);
    };
    options.Retry.OnRetry = args =>
    {
        var requestUri = args.Outcome.Result?.RequestMessage?.RequestUri?.ToString() ?? "<unknown>";
        Log.Warning(
            "Retrying {Client} request to {Url}. Attempt {AttemptNumber}.",
            clientName,
            requestUri,
            args.AttemptNumber + 1);
        return ValueTask.CompletedTask;
    };

    options.CircuitBreaker.ShouldHandle = args =>
    {
        var isTransient = HttpClientResiliencePredicates.IsTransient(args.Outcome);
        var isRateLimited = args.Outcome.Result?.StatusCode == HttpStatusCode.TooManyRequests;
        return ValueTask.FromResult(isTransient || isRateLimited);
    };
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 1.0;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);

    options.TotalRequestTimeout.Timeout = totalTimeout;
}
