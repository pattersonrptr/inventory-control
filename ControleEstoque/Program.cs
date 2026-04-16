using AspNetCoreRateLimit;
using ControleEstoque.Authentication;
using ControleEstoque.Data;
using ControleEstoque.Models;
using ControleEstoque.Repositories;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi;

// Allow DateTime with Kind=Unspecified to be sent to PostgreSQL without requiring UTC conversion.
// This is needed because HTML date inputs and DateTime.Today produce Unspecified-kind values.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Prevents navigation properties of EF (not posted by the form) from generating
    // implicit [Required] errors for non-nullable reference types.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    // Require authenticated users globally — use [AllowAnonymous] to opt out
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// Support pt-BR culture: allows decimals with comma (1.234,56) and R$ currency
var ptBR = new System.Globalization.CultureInfo("pt-BR");
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.DefaultModelBindingMessageProvider>(p =>
    p.SetValueMustBeANumberAccessor(fn => $"O campo {fn} deve ser um número válido."));
builder.Services.AddRequestLocalization(opts =>
{
    opts.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(ptBR);
    opts.SupportedCultures = new[] { ptBR };
    opts.SupportedUICultures = new[] { ptBR };
});

// EF Core configuration.
// Provider is selected at runtime based on the connection string format:
//   - PostgreSQL: "Host=..." (production / Docker)
//   - SQLite:     "Data Source=..." (local development fallback)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "Set the environment variable ConnectionStrings__DefaultConnection " +
        "or add it to appsettings.json.");
var usePostgres = connectionString.StartsWith("Host=", StringComparison.OrdinalIgnoreCase)
               || connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
               || connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    if (usePostgres)
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);

    // Suppress false positive: migrations are generated with SQLite but may run on PostgreSQL.
    // The provider difference causes EF Core 10 to flag the snapshot as stale.
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

    options.AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>());
});

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// API key authentication (separate scheme for REST API endpoints)
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", null);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Inventory Control API",
        Version = "v1",
        Description = "REST API for inventory management"
    });
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API key via X-Api-Key header",
        Name = "X-Api-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "ApiKey"
    });
    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("ApiKey"),
            new List<string>()
        }
    });
});

// Health checks (used by Docker/TrueNAS to verify the app + DB are working)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// IP rate limiting on API endpoints
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.GeneralRules =
    [
        new RateLimitRule
        {
            Endpoint = "post:/api/*",
            Period = "1m",
            Limit = 30
        },
        new RateLimitRule
        {
            Endpoint = "get:/api/*",
            Period = "1m",
            Limit = 60
        }
    ];
});
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

// Dependency injection for repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();
builder.Services.AddScoped<IProcessedOrderRepository, ProcessedOrderRepository>();

// Conditional registration of the e-commerce integration layer
// Supports multiple stores — each entry in the "Stores" array is an independent store connection.
// Backward compatibility: if "Integration" section exists (single store), it is auto-migrated.
var storesConfig = builder.Configuration
    .GetSection("Stores")
    .Get<List<ControleEstoque.Integrations.Abstractions.IntegrationConfig>>()
    ?? new List<ControleEstoque.Integrations.Abstractions.IntegrationConfig>();

// Backward compatibility: migrate legacy single-store "Integration" config
var legacyConfig = builder.Configuration
    .GetSection("Integration")
    .Get<ControleEstoque.Integrations.Abstractions.IntegrationConfig>();
if (legacyConfig?.Enabled == true && !storesConfig.Any(s => s.Enabled))
{
    if (string.IsNullOrEmpty(legacyConfig.Name))
        legacyConfig.Name = legacyConfig.Platform;
    var legacyInterval = builder.Configuration.GetValue<int?>("Integration:OrderSyncIntervalMinutes");
    if (legacyInterval.HasValue)
        legacyConfig.OrderSyncIntervalMinutes = legacyInterval.Value;
    storesConfig.Add(legacyConfig);
}

builder.Services.AddSingleton(storesConfig);

// Platform factory registry — register all known platform adapters
builder.Services.AddSingleton<ControleEstoque.Integrations.Abstractions.IPlatformFactory,
    ControleEstoque.Integrations.NuvemshopPlatformFactory>();

// Named HttpClient per platform with resilience handlers
builder.Services.AddHttpClient("Platform_nuvemshop")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
    });

builder.Services.AddSingleton<ControleEstoque.Integrations.PlatformRegistry>();
builder.Services.AddScoped<ControleEstoque.Integrations.SyncServiceFactory>();

// Background order sync runs for all enabled stores
if (storesConfig.Any(s => s.Enabled))
{
    builder.Services.AddHostedService<ControleEstoque.BackgroundServices.OrderSyncBackgroundService>();
}

// Low stock email notifications (runs independently of e-commerce integration)
if (builder.Configuration.GetValue<bool?>("EmailNotifications:Enabled") == true)
{
    builder.Services.AddHostedService<ControleEstoque.BackgroundServices.LowStockNotificationService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Swagger UI available in all environments at /swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Control API v1");
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseIpRateLimiting();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health");

// Apply pending migrations automatically on startup (relational providers only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();

    // Fix auto-increment columns on PostgreSQL.
    // Migrations generated with SQLite use "Sqlite:Autoincrement" which Npgsql ignores,
    // leaving integer PK columns without SERIAL/IDENTITY. Add sequences idempotently.
    if (usePostgres)
    {
        // Fix boolean columns: SQLite migrations create them as INTEGER, but Npgsql sends boolean values.
        // Convert INTEGER columns that should be boolean to proper boolean type.
        var fixBooleanColumnsSql = """
            DO $$
            DECLARE
                tbl  TEXT;
                col  TEXT;
            BEGIN
                FOR tbl, col IN
                    VALUES ('AspNetUsers','EmailConfirmed'),
                           ('AspNetUsers','PhoneNumberConfirmed'),
                           ('AspNetUsers','TwoFactorEnabled'),
                           ('AspNetUsers','LockoutEnabled')
                LOOP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = tbl AND column_name = col AND data_type = 'integer'
                    ) THEN
                        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE boolean USING %I::boolean', tbl, col, col);
                    END IF;
                END LOOP;
            END $$;
            """;
        db.Database.ExecuteSqlRaw(fixBooleanColumnsSql);

        var fixSequenceSql = """
            DO $$
            DECLARE
                tbl  TEXT;
                col  TEXT;
                seq  TEXT;
                typ  TEXT;
                max_val BIGINT;
            BEGIN
                -- (table, column, sequence_name, pg_type)
                FOR tbl, col, seq, typ IN
                    VALUES ('AuditLogs','Id','auditlogs_id_seq','bigint'),
                           ('AspNetRoleClaims','Id','aspnetroleclaims_id_seq','integer'),
                           ('AspNetUserClaims','Id','aspnetuserclaims_id_seq','integer')
                LOOP
                    IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = seq) THEN
                        EXECUTE format('CREATE SEQUENCE %I AS %s OWNED BY %I.%I', seq, typ, tbl, col);
                        EXECUTE format('SELECT COALESCE(MAX(%I),0)+1 FROM %I', col, tbl) INTO max_val;
                        PERFORM setval(seq, max_val, false);
                        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I SET DEFAULT nextval(%L)', tbl, col, seq);
                    END IF;
                END LOOP;
            END $$;
            """;
        db.Database.ExecuteSqlRaw(fixSequenceSql);
    }

    // Seed roles and default admin user
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    string[] roles = ["Admin", "Operator"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var adminEmail = builder.Configuration["DefaultAdmin:Email"] ?? "admin@inventory.local";
    var adminPassword = builder.Configuration["DefaultAdmin:Password"] ?? "Admin123!";
    var adminFullName = builder.Configuration["DefaultAdmin:FullName"] ?? "Administrador";

    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = adminFullName,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();

// Make the auto-generated Program class accessible to integration tests
public partial class Program { }
