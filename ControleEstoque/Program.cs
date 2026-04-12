using ControleEstoque.Data;
using ControleEstoque.Repositories;
using ControleEstoque.Repositories.Interfaces;
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

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (usePostgres)
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});

// Dependency injection for repositories
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IStockMovementRepository, StockMovementRepository>();

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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Apply pending migrations automatically on startup (relational providers only)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
}

app.Run();

// Make the auto-generated Program class accessible to integration tests
public partial class Program { }
