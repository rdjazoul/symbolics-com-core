using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Symbolics.Com.Core.Api.Logging;
using Symbolics.Com.Core.Application.Repositories;
using Symbolics.Com.Core.Contract.ExternalServices;
using Symbolics.Com.Core.Contract.Qdrant;
using Symbolics.Com.Core.Infrastructure.ExternalServices;
using Symbolics.Com.Core.Infrastructure.Persistence;
using Symbolics.Com.Core.Infrastructure.Qdrant;
using Symbolics.Com.Core.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<LogOptions>(builder.Configuration.GetSection("LogOptions"));

var levelSwitch = new LoggingLevelSwitch();
builder.Services.AddSingleton(levelSwitch);

builder.Host.UseSerilog((context, services, configuration) =>
{
    var optionsMonitor = services.GetRequiredService<IOptionsMonitor<LogOptions>>();
    var options = optionsMonitor.CurrentValue;
    ConfigureLogger(configuration, context.Configuration, options, levelSwitch);
});

var connectionString = builder.Configuration.GetConnectionString("CoreDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'CoreDatabase' is missing from configuration.");
}

builder.Services.AddDbContext<CoreDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.Configure<TwitchOptions>(builder.Configuration.GetSection("Twitch"));
builder.Services.Configure<QdrantOptions>(builder.Configuration.GetSection("Qdrant"));
builder.Services.AddHttpClient<QdrantClient>();
builder.Services.AddSingleton<IQdrantClient>(sp => sp.GetRequiredService<QdrantClient>());
builder.Services.AddHttpClient<ITwitchService, TwitchService>();
builder.Services.AddHttpClient<IAiService, GeminiAiService>();
builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();
builder.Services.AddHostedService<QdrantCollectionInitializer>();
builder.Services.AddControllers();

var app = builder.Build();

var logOptionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<LogOptions>>();
logOptionsMonitor.OnChange(options =>
{
    levelSwitch.MinimumLevel = options.MinimumLevel;
    Log.CloseAndFlush();
    Log.Logger = ConfigureLogger(new LoggerConfiguration(), app.Configuration, options, levelSwitch).CreateLogger();
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

app.Run();

static LoggerConfiguration ConfigureLogger(
    LoggerConfiguration configuration,
    IConfiguration appConfiguration,
    LogOptions options,
    LoggingLevelSwitch levelSwitch)
{
    levelSwitch.MinimumLevel = options.MinimumLevel;

    configuration
        .ReadFrom.Configuration(appConfiguration)
        .MinimumLevel.ControlledBy(levelSwitch)
        .Enrich.FromLogContext();

    if (!string.IsNullOrWhiteSpace(options.SeqUrl))
    {
        configuration.WriteTo.Seq(options.SeqUrl);
    }

    return configuration;
}
