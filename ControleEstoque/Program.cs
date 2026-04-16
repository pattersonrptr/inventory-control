using ControleEstoque.Data;
using ControleEstoque.Models;
using ControleEstoque.Repositories;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

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

// Health checks (used by Docker/TrueNAS to verify the app + DB are working)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Dependency injection for repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();
builder.Services.AddScoped<IProcessedOrderRepository, ProcessedOrderRepository>();

// Conditional registration of the e-commerce integration layer
var integrationConfig = builder.Configuration
    .GetSection("Integration")
    .Get<ControleEstoque.Integrations.Abstractions.IntegrationConfig>();

if (integrationConfig?.Enabled == true)
{
    builder.Services.AddSingleton(integrationConfig);
    builder.Services.AddHttpClient<ControleEstoque.Integrations.Nuvemshop.NuvemshopClient>();
    builder.Services.AddScoped<ControleEstoque.Integrations.Abstractions.IStoreIntegration,
        ControleEstoque.Integrations.Nuvemshop.NuvemshopIntegration>();
    builder.Services.AddScoped<ControleEstoque.Integrations.SyncService>();
    builder.Services.AddHostedService<ControleEstoque.BackgroundServices.OrderSyncBackgroundService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();
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
