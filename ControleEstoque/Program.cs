using ControleEstoque.Data;
using ControleEstoque.Repositories;
using ControleEstoque.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Prevents navigation properties of EF (not posted by the form) from generating
    // implicit [Required] errors for non-nullable reference types.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
});

// Support en-US culture: allows decimals with dot (1,234.56)
var enUS = new System.Globalization.CultureInfo("en-US");
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ModelBinding.Metadata.DefaultModelBindingMessageProvider>(p =>
    p.SetValueMustBeANumberAccessor(fn => $"The field {fn} must be a valid number."));
builder.Services.AddRequestLocalization(opts =>
{
    opts.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(enUS);
    opts.SupportedCultures = new[] { enUS };
    opts.SupportedUICultures = new[] { enUS };
});

// EF Core configuration.
// Development: local SQLite (no server dependency).
// Production: add the desired provider to .csproj and swap UseSqlite below.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

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

app.Run();
