using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Data.SqlClient;
using OpsPilot.Api.Infrastructure;
using OpsPilot.Api.Services;
using OpsPilot.Application;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Security;
using OpsPilot.Application.Jobs;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.Identity;
using OpsPilot.Infrastructure.Jobs;
using OpsPilot.Infrastructure.Persistence;
using OpsPilot.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

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

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeeder>().SeedAsync();

    // Demo data is for development and demo containers only; never on by default in production.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Seed:DemoData"))
    {
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
    }
}

if (jobsEnabled)
{
    HangfireJobScheduler.RegisterRecurringJobs(app.Services.GetRequiredService<IRecurringJobManager>(), TimeZoneInfo.Local);
}

app.UseExceptionHandler();

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
