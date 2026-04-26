using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi;
using Serilog;

// Allow DateTime with Kind=Unspecified to be sent to PostgreSQL without requiring UTC conversion.
// This is needed because HTML date inputs and DateTime.Today produce Unspecified-kind values.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig.ReadFrom.Configuration(context.Configuration);

    // Ensure a file sink is always active (config may be absent in production image)
    var hasFileSink = context.Configuration
        .GetSection("Serilog:WriteTo")
        .GetChildren()
        .Any(s => s["Name"] == "File");

    if (!hasFileSink)
    {
        loggerConfig.WriteTo.File(
            path: "logs/inventory-.log",
            rollingInterval: Serilog.RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 52_428_800,
            rollOnFileSizeLimit: true,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
});

builder.Services.AddControllersWithViews(options =>
{
    // Prevents navigation properties of EF (not posted by the form) from generating
    // implicit [Required] errors for non-nullable reference types.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    // Require authenticated users globally — use [AllowAnonymous] to opt out
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

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
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 10;
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

    // Return 401/403 for API paths instead of redirecting to the login page.
    // Without this, MVC redirects API callers to the HTML login page (302 → 200).
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
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
    .Get<List<IntegrationConfig>>()
    ?? new List<IntegrationConfig>();

// Backward compatibility: migrate legacy single-store "Integration" config
var legacyConfig = builder.Configuration
    .GetSection("Integration")
    .Get<IntegrationConfig>();
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
builder.Services.AddSingleton<IPlatformFactory,
    NuvemshopPlatformFactory>();

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

builder.Services.AddSingleton<PlatformRegistry>();
builder.Services.AddScoped<SyncServiceFactory>();

// Background order sync runs for all enabled stores
if (storesConfig.Any(s => s.Enabled))
{
    builder.Services.AddHostedService<OrderSyncBackgroundService>();
}

// Low stock email notifications (runs independently of e-commerce integration)
if (builder.Configuration.GetValue<bool?>("EmailNotifications:Enabled") == true)
{
    builder.Services.AddHostedService<LowStockNotificationService>();
}

// AuditLog retention cleanup (runs daily)
builder.Services.AddHostedService<AuditLogCleanupService>();

// Manual database backup
builder.Services.AddSingleton<IClock, InventoryControl.Infrastructure.SystemClock>();
builder.Services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
builder.Services.AddScoped<IOffsiteBackupService, OffsiteBackupService>();
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IDomainEventHandler<StockChanged>, InventoryControl.Features.Sync.Handlers.PushStockOnStockChange>();

var app = builder.Build();

// Warn if the rclone config file is world-readable (Linux deployments)
if (OperatingSystem.IsLinux())
{
    var rcloneConfigPath = builder.Configuration["OffsiteBackup:RcloneConfigPath"];
    if (!string.IsNullOrEmpty(rcloneConfigPath) && File.Exists(rcloneConfigPath))
    {
        var fileMode = File.GetUnixFileMode(rcloneConfigPath);
        if ((fileMode & UnixFileMode.OtherRead) != 0)
        {
            var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
            startupLog.LogWarning(
                "SECURITY: rclone config '{Path}' is world-readable. Run: chmod 600 \"{Path}\"",
                rcloneConfigPath, rcloneConfigPath);
        }
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
            }
            else
            {
                context.Response.Redirect("/Home/Error");
            }
        });
    });
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Control API v1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
app.UseSerilogRequestLogging();
app.UseIpRateLimiting();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// /health/live  — basic ping, anonymous, used by Docker/TrueNAS liveness probes
// /health/ready — includes DbContext check, used by readiness probes and monitoring
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // no individual checks — just confirms the process is alive
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true   // all registered checks including DbContext
}).AllowAnonymous();

// Database initialisation is intentionally NOT run on normal startup.
// Use:  dotnet run -- migrate   (or dotnet InventoryControl.dll migrate)
// This applies pending migrations, PostgreSQL fixes, and seeds roles/admin/dev data.
// In docker-compose the dedicated 'db-migrate' service handles this before the app starts.
if (args.Contains("migrate"))
{
    await ApplyMigrationsAsync(app.Services, usePostgres, app.Configuration, app.Environment);
    return;
}

app.Run();

static async Task ApplyMigrationsAsync(
    IServiceProvider services, bool usePostgres,
    IConfiguration configuration, IWebHostEnvironment environment)
{
    await using var scope = services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (db.Database.IsRelational())
        db.Database.Migrate();

    // Fix auto-increment columns on PostgreSQL.
    // Migrations generated with SQLite use "Sqlite:Autoincrement" which Npgsql ignores,
    // leaving integer PK columns without SERIAL/IDENTITY. Add sequences idempotently.
    if (usePostgres)
    {
        // Fix DateTime columns: SQLite migrations create them as TEXT, but Npgsql requires proper
        // timestamp types for date operations (date_part, comparisons, etc.).
        var fixDateTimeColumnsSql = """
            DO $$
            DECLARE
                tbl  TEXT;
                col  TEXT;
                typ  TEXT;
            BEGIN
                FOR tbl, col, typ IN
                    VALUES ('AuditLogs','Timestamp','timestamp without time zone'),
                           ('ProcessedOrders','ProcessedAt','timestamp without time zone'),
                           ('SyncStates','LastProcessedAt','timestamp without time zone'),
                           ('StockMovements','Date','timestamp without time zone'),
                           ('AspNetUsers','LockoutEnd','timestamp with time zone')
                LOOP
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_name = tbl AND column_name = col AND data_type = 'text'
                    ) THEN
                        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE %s USING %I::%s', tbl, col, typ, col, typ);
                    END IF;
                END LOOP;
            END $$;
            """;
        db.Database.ExecuteSqlRaw(fixDateTimeColumnsSql);

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
                           ('AspNetUsers','LockoutEnabled'),
                           ('ProductImages','IsPrimary')
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
                    VALUES ('Categories','Id','categories_id_seq','integer'),
                           ('Suppliers','Id','suppliers_id_seq','integer'),
                           ('Products','Id','products_id_seq','integer'),
                           ('StockMovements','Id','stockmovements_id_seq','integer'),
                           ('ProcessedOrders','Id','processedorders_id_seq','integer'),
                           ('ProductImages','Id','productimages_id_seq','integer'),
                           ('AuditLogs','Id','auditlogs_id_seq','bigint'),
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

    var adminEmail = configuration["DefaultAdmin:Email"] ?? "admin@inventory.local";
    var adminPassword = configuration["DefaultAdmin:Password"] ?? "Admin1234!@";
    var adminFullName = configuration["DefaultAdmin:FullName"] ?? "Administrador";

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
        else
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError("Failed to seed admin account {Email}: {Errors}", adminEmail, errors);
        }
    }

    if (environment.IsDevelopment())
        await SeedDevelopmentDataAsync(db);
}

static async Task SeedDevelopmentDataAsync(AppDbContext db)
{
    if (await db.Categories.AnyAsync()) return;

    // Categories (hierarchical)
    var escrita = new Category { Name = "ESCRITA", Description = "Materiais de escrita" };
    var acessorios = new Category { Name = "ACESSÓRIOS", Description = "Acessórios diversos" };
    var papelaria = new Category { Name = "PAPELARIA", Description = "Materiais de papelaria em geral" };
    db.Categories.AddRange(escrita, acessorios, papelaria);
    await db.SaveChangesAsync();

    var canetas = new Category { Name = "CANETAS", Description = "Canetas de todos os tipos", ParentId = escrita.Id };
    var lapisCat = new Category { Name = "LÁPIS", Description = "Lápis e lapiseiras", ParentId = escrita.Id };
    db.Categories.AddRange(canetas, lapisCat);
    await db.SaveChangesAsync();

    // Suppliers
    var aliexpress = new Supplier { Name = "ALIEXPRESS", Notes = "Importação direta, prazo ~30 dias", LeadTimeDays = 30 };
    var starAtacado = new Supplier { Name = "STAR ATACADO", Notes = "Atacado papelaria" };
    var canetasAtacado = new Supplier { Name = "CANETAS ATACADO", Notes = "Especializado em canetas" };
    var temu = new Supplier { Name = "TEMU", Notes = "Importação, prazo variável", LeadTimeDays = 25 };
    var descontoAqui = new Supplier { Name = "DESCONTO AQUI" };
    var papeleraAtacado = new Supplier { Name = "PAPELERA ATACADO" };
    db.Suppliers.AddRange(aliexpress, starAtacado, canetasAtacado, temu, descontoAqui, papeleraAtacado);
    await db.SaveChangesAsync();

    // Products (sample from the spreadsheet)
    var products = new List<Product>
    {
        new() { Name = "Caneta gel Dogs - Nervosa", Sku = "001-CG-DOGN-ARV", Brand = "IMPORTADA", CostPrice = 5.00m, SellingPrice = 12.90m, MinimumStock = 2, CurrentStock = 3, CategoryId = canetas.Id },
        new() { Name = "Caneta gel Dogs - Rolou", Sku = "002-CG-DOGR-ARV", Brand = "IMPORTADA", CostPrice = 5.00m, SellingPrice = 12.90m, MinimumStock = 2, CurrentStock = 2, CategoryId = canetas.Id },
        new() { Name = "Caneta gel Gato Musical", Sku = "003-CG-GATM-PT", Brand = "IMPORTADA", CostPrice = 2.10m, SellingPrice = 7.90m, MinimumStock = 3, CurrentStock = 5, CategoryId = canetas.Id },
        new() { Name = "Caneta gel Coração", Sku = "004-CG-COR-RS", Brand = "IMPORTADA", CostPrice = 2.50m, SellingPrice = 8.90m, MinimumStock = 2, CurrentStock = 4, CategoryId = canetas.Id },
        new() { Name = "Caneta esferográfica BRW 0.7 Azul", Sku = "005-CE-BRW-AZ", Brand = "BRW", CostPrice = 1.20m, SellingPrice = 3.50m, MinimumStock = 5, CurrentStock = 10, CategoryId = canetas.Id },
        new() { Name = "Caneta esferográfica BRW 0.7 Preta", Sku = "006-CE-BRW-PT", Brand = "BRW", CostPrice = 1.20m, SellingPrice = 3.50m, MinimumStock = 5, CurrentStock = 8, CategoryId = canetas.Id },
        new() { Name = "Caneta esferográfica BRW 0.7 Vermelha", Sku = "007-CE-BRW-VM", Brand = "BRW", CostPrice = 1.20m, SellingPrice = 3.50m, MinimumStock = 5, CurrentStock = 6, CategoryId = canetas.Id },
        new() { Name = "Caneta Molin retro gel pastel (kit 5)", Sku = "008-CG-MOLIN-KIT5", Brand = "MOLIN", CostPrice = 15.00m, SellingPrice = 29.90m, MinimumStock = 2, CurrentStock = 3, CategoryId = canetas.Id },
        new() { Name = "Caneta CIS apagável azul", Sku = "009-CA-CIS-AZ", Brand = "CIS", CostPrice = 8.50m, SellingPrice = 16.90m, MinimumStock = 3, CurrentStock = 5, CategoryId = canetas.Id },
        new() { Name = "Caneta CIS apagável preta", Sku = "010-CA-CIS-PT", Brand = "CIS", CostPrice = 8.50m, SellingPrice = 16.90m, MinimumStock = 3, CurrentStock = 4, CategoryId = canetas.Id },
        new() { Name = "Caneta TRIS gel neon (kit 6)", Sku = "011-CG-TRIS-NEON6", Brand = "TRIS", CostPrice = 12.00m, SellingPrice = 24.90m, MinimumStock = 2, CurrentStock = 2, CategoryId = canetas.Id },
        new() { Name = "Caneta Tilibra gel glitter 1.0 (kit 8)", Sku = "012-CG-TILIBRA-GLIT8", Brand = "TILIBRA", CostPrice = 20.00m, SellingPrice = 39.90m, MinimumStock = 1, CurrentStock = 3, CategoryId = canetas.Id },
        new() { Name = "Caneta Fofy gel 0.5 pastel rosa", Sku = "013-CG-FOFY-RS", Brand = "FOFY", CostPrice = 3.50m, SellingPrice = 8.90m, MinimumStock = 3, CurrentStock = 5, CategoryId = canetas.Id },
        new() { Name = "Caneta Leonora brush pen (kit 12)", Sku = "014-CB-LEONORA-KIT12", Brand = "LEONORA", CostPrice = 25.00m, SellingPrice = 49.90m, MinimumStock = 1, CurrentStock = 2, CategoryId = canetas.Id },
        new() { Name = "Caneta Compactor esferográfica 0.7 azul (cx 50)", Sku = "015-CE-COMP-AZ50", Brand = "COMPACTOR", CostPrice = 35.00m, SellingPrice = 59.90m, MinimumStock = 1, CurrentStock = 1, CategoryId = canetas.Id },
        new() { Name = "Lápis preto HB TRIS (cx 12)", Sku = "016-LP-TRIS-HB12", Brand = "TRIS", CostPrice = 6.00m, SellingPrice = 14.90m, MinimumStock = 2, CurrentStock = 4, CategoryId = lapisCat.Id },
        new() { Name = "Borracha branca Tilibra (kit 3)", Sku = "017-BR-TILIBRA-KIT3", Brand = "TILIBRA", CostPrice = 2.50m, SellingPrice = 6.90m, MinimumStock = 3, CurrentStock = 6, CategoryId = papelaria.Id },
        new() { Name = "Apontador com depósito CIS", Sku = "018-AP-CIS-DEP", Brand = "CIS", CostPrice = 3.00m, SellingPrice = 7.90m, MinimumStock = 3, CurrentStock = 5, CategoryId = acessorios.Id },
        new() { Name = "Régua 30cm transparente Molin", Sku = "019-RG-MOLIN-30", Brand = "MOLIN", CostPrice = 1.50m, SellingPrice = 4.50m, MinimumStock = 3, CurrentStock = 8, CategoryId = acessorios.Id },
        new() { Name = "Fita corretiva BRW 5mmx6m", Sku = "020-FC-BRW-5X6", Brand = "BRW", CostPrice = 3.80m, SellingPrice = 8.90m, MinimumStock = 2, CurrentStock = 4, CategoryId = acessorios.Id },
    };

    db.Products.AddRange(products);
    await db.SaveChangesAsync();
}

// Make the auto-generated Program class accessible to integration tests
public partial class Program { }
