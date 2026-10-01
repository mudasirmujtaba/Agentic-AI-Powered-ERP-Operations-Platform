using System.Text.Json.Serialization;
using OpsPilot.Api.Infrastructure;
using OpsPilot.Api.Services;
using OpsPilot.Application;
using OpsPilot.Application.Common.Interfaces;
using OpsPilot.Application.Common.Security;
using OpsPilot.Infrastructure;
using OpsPilot.Infrastructure.Identity;
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

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(frontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
