using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudioCRM.Application.Interfaces;
using StudioCRM.Application.Settings;
using StudioCRM.Infrastructure.Persistence;
using StudioCRM.Infrastructure.Seed;
using StudioCRM.Infrastructure.Services;
using System.Text;
using Microsoft.OpenApi.Models;
using StudioCRM.Api.Swagger;
using System.Security.Claims;
using Resend;
using StudioCRM.Api.BackgroundServices;
using StudioCRM.Application.Interfaces.Calendar;
using StudioCRM.Application.Interfaces.Mail;
using StudioCRM.Infrastructure.Services.Calendar;
using StudioCRM.Infrastructure.Services.Mail;
using StudioCRM.Infrastructure.Services.Storage;
using StudioCRM.Application.ClientPackages.Services;
using StudioCRM.Application.Interfaces.Storage;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var defaultConnectionName = builder.Environment.IsDevelopment()
    ? "TestConnection"
    : "DefaultConnection";
var connectionName = builder.Configuration["Database:ConnectionName"] ?? defaultConnectionName;

// JWT settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("App"));
builder.Services.Configure<CloudflareR2Settings>(
    builder.Configuration.GetSection("CloudflareR2"));
// Database
var connectionString = builder.Configuration.GetConnectionString(connectionName)
    ?? throw new InvalidOperationException($"Connection string '{connectionName}' is not configured.");

builder.Services.AddDbContext<StudioCRMDbContext>(options =>
    options.UseNpgsql(connectionString));

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITrainerService, TrainerService>();
builder.Services.AddScoped<ITrainerContractService, TrainerContractService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IPackageService, PackageService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IClientPortalService, ClientPortalService>();
builder.Services.AddScoped<ITrainerPortalService, TrainerPortalService>();
builder.Services.AddScoped<IExternalCalendarEventService, ExternalCalendarEventService>();
builder.Services.AddScoped<ISessionParticipantService, SessionParticipantService>();
builder.Services.AddScoped<ITrainerRateService, TrainerRateService>();
builder.Services.AddScoped<ITrainerSettlementService, TrainerSettlementService>();
builder.Services.AddScoped<IMilestoneService, MilestoneService>();
builder.Services.AddScoped<IClientPackageService, ClientPackageService>();
builder.Services.AddScoped<IClientPaymentService, ClientPaymentService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IOperationalAlertService, OperationalAlertService>();
builder.Services.AddScoped<IStudioSettingsService, StudioSettingsService>();
builder.Services.AddScoped<IOutlookContactService, OutlookContactService>();
builder.Services.AddScoped<ISessionAutoCompletionService, SessionAutoCompletionService>();
builder.Services.AddScoped<ITrainingPlanFileService, TrainingPlanFileService>();
builder.Services.AddHttpClient<IObjectStorageService, CloudflareR2ObjectStorageService>();
// Authentication
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings.Key)),
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name
    };
});

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "StudioCRM.Api",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Wpisz token JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.Configure<ResendSettings>(
    builder.Configuration.GetSection("Resend"));

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("Email"));

builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("App"));

builder.Services.AddOptions();

builder.Services.AddHttpClient<ResendClient>();

builder.Services.Configure<OutlookSettings>(
builder.Configuration.GetSection("Outlook"));

builder.Services.AddHttpClient<IOutlookCalendarAuthService, OutlookCalendarAuthService>();
builder.Services.AddHttpClient<IOutlookCalendarSyncService, OutlookCalendarSyncService>();
builder.Services.AddHttpClient<IOutlookSubscriptionService, OutlookSubscriptionService>();
builder.Services.AddHttpClient<IOutlookWebhookService, OutlookWebhookService>();
builder.Services.AddHttpClient<IOutlookTokenService, OutlookTokenService>();

builder.Services.AddHostedService<OutlookSubscriptionRenewalWorker>();
builder.Services.AddHostedService<SessionAutoCompletionWorker>();

builder.Services.Configure<ResendClientOptions>(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiToken"]!;
});

builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        var (statusCode, message) = exception switch
        {
            InvalidOperationException invalidOperationException => (
                StatusCodes.Status400BadRequest,
                invalidOperationException.Message),
            ArgumentException argumentException => (
                StatusCodes.Status400BadRequest,
                argumentException.Message),
            UnauthorizedAccessException unauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                unauthorizedAccessException.Message),
            KeyNotFoundException keyNotFoundException => (
                StatusCodes.Status404NotFound,
                keyNotFoundException.Message),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The data was changed by another operation. Refresh and try again."),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "Database conflict. Check whether the resource is still connected with existing data."),
            HttpRequestException httpRequestException => (
                StatusCodes.Status502BadGateway,
                app.Environment.IsDevelopment()
                    ? httpRequestException.Message
                    : "External service request failed."),
            _ => (
                StatusCodes.Status500InternalServerError,
                app.Environment.IsDevelopment()
                    ? exception?.Message ?? "Unexpected server error."
                    : "Unexpected server error.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        if (exception is not null && statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled request exception.");
        }
        else if (exception is not null)
        {
            logger.LogWarning(exception, "Handled request exception returned {StatusCode}.", statusCode);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            message
        }));
    });
});

// Swagger

    app.UseSwagger();
    app.UseSwaggerUI();


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapMethods("/health", new[] { "GET", "HEAD" }, () => Results.Ok(new
{
    status = "ok",
    utcNow = DateTime.UtcNow
}));

app.MapMethods(
    "/api/workers/session-auto-completion/keepalive",
    new[] { "GET", "HEAD" },
    async (
        ISessionAutoCompletionService sessionAutoCompletionService,
        CancellationToken cancellationToken) =>
    {
        var result = await sessionAutoCompletionService.CompleteFinishedSessionsAsync(cancellationToken);
        var response = new
        {
            message = "Session auto-completion checked",
            completed = result.CompletedCount,
            skipped = result.SkippedCount,
            failed = result.FailedCount,
            utcNow = DateTime.UtcNow
        };

        return result.FailedCount > 0
            ? Results.Json(response, statusCode: StatusCodes.Status500InternalServerError)
            : Results.Ok(response);
    });

app.MapControllers();

// Seeder
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<StudioCRMDbContext>();
    var seedDemoData = builder.Configuration.GetValue<bool>("Seed:DemoData") &&
                       builder.Configuration["Seed:DemoDataConfirmation"] == "ALLOW_DEMO_SEED";

    await DataSeeder.SeedAsync(dbContext, seedDemoData);
}
app.Run();
