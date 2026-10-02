using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using OpsPilot.Api.Infrastructure;
using OpsPilot.Api.Services;
using OpsPilot.Application;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Observability;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Jobs;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Jobs;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Observability (design doc §42). Structured logs via Serilog: readable in development, compact JSON when
// Logging:Json is set (containers), always with the trace id so logs and traces line up.
builder.Host.UseSerilog((context, services, logger) =>
{
    logger
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "OpsPilot.Api");

    if (context.Configuration.GetValue<bool>("Logging:Json"))
    {
        logger.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
    else
    {
        logger.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} {TraceId}{NewLine}{Exception}");
    }
});

// Traces and metrics via OpenTelemetry; exported over OTLP when OTEL_EXPORTER_OTLP_ENDPOINT is set
// (e.g. the Jaeger container in docker-compose's "observability" profile).
var otel = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("opspilot-api"))
    .WithTracing(tracing => tracing
        .AddSource(OpsPilotTelemetry.Name)
        .AddAspNetCoreInstrumentation(options => options.Filter = http => !http.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddMeter(OpsPilotTelemetry.Name)
        .AddMeter("System.Runtime")
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    otel.UseOtlpExporter();
}
builder.Services.AddSingleton<MetricsSnapshot>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MetricsSnapshot>());

const string frontendCorsPolicy = "Frontend";

builder.Services
    .AddControllers(options =>
    {
        // FluentValidation is the single source of request validation, so MVC shouldn't add its own
        // implicit [Required] errors for non-nullable properties.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Background jobs (design doc §40): Hangfire on the application database, in its own schema.
var jobsEnabled = builder.Configuration.GetValue("Jobs:Enabled", true);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseFilter(new JobTelemetryFilter())
    .UseSqlServerStorage(() => new SqlConnection(connectionString), new SqlServerStorageOptions
    {
        SchemaName = "hangfire",
        PrepareSchemaIfNecessary = true,
        QueuePollInterval = TimeSpan.FromSeconds(15),
    }));
if (jobsEnabled)
{
    // Few, short jobs: two workers are plenty and keep the footprint small.
    builder.Services.AddHangfireServer(options => options.WorkerCount = 2);
}
builder.Services.AddScoped<IJobScheduler, HangfireJobScheduler>();

var authorization = builder.Services.AddAuthorizationBuilder();
foreach (var (policy, roles) in Policies.RolesByPolicy)
{
    authorization.AddPolicy(policy, p => p.RequireRole(roles));
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Migrations, roles and seed data. A database that is still starting (a fresh container, or a busy server) times
// out rather than refusing; retry with backoff instead of failing the whole start-up. Every step is idempotent.
const int startupAttempts = 6;
for (var attempt = 1; ; attempt++)
{
    try
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

        // Demo data is for development and demo containers only; never on by default in production.
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:DemoData"))
        {
            await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
        }
        break;
    }
    catch (SqlException ex) when (attempt < startupAttempts)
    {
        var delay = TimeSpan.FromSeconds(5 * attempt);
        app.Logger.LogWarning("Database not ready (attempt {Attempt}/{Attempts}: {Message}); retrying in {Delay}s",
            attempt, startupAttempts, ex.Message, delay.TotalSeconds);
        await Task.Delay(delay);
    }
}

if (jobsEnabled)
{
    HangfireJobScheduler.RegisterRecurringJobs(app.Services.GetRequiredService<IRecurringJobManager>(), TimeZoneInfo.Local);
}

app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
    options.GetLevel = (http, elapsed, ex) =>
        ex is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose
        : elapsed > 2000 ? LogEventLevel.Warning
        : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnostics, http) =>
    {
        diagnostics.Set("UserId", http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        diagnostics.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
    };
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Hangfire's own dashboard, for local debugging only (local requests, no JWT). The app's Automation page
    // (/api/jobs) is the role-checked surface.
    app.UseHangfireDashboard("/hangfire");
}

app.UseHttpsRedirection();

app.UseCors(frontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
