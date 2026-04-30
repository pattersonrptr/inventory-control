using InventoryControl.Infrastructure.Persistence;
using InventoryControl.Infrastructure.Persistence.Repositories;
using InventoryControl.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryControl.Tests.Unit.Repositories;

/// <summary>
/// Repros the bug where editing a product makes its existing images disappear.
/// Mirrors what happens in ProductsController.Edit POST: each HTTP request gets
/// a fresh DbContext, but the same SQLite database persists across them.
///   1. Seed: a product + 1 existing image.
///   2. Edit POST: a fresh DbContext receives a form-bound Product (Images = empty list).
///   3. Controller calls _productRepo.UpdateAsync(product) which does
///      _context.Products.Update(product) + SaveChangesAsync.
///   4. Verify: the existing image is still in the DB.
/// </summary>
public class ProductRepositoryEditFlowTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProductRepositoryEditFlowTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new AppDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task EditPostFlow_AddNewImageThenUpdateAsync_DoesNotLoseImages()
    {
        // Seed: product with 1 existing image
        int productId;
        await using (var seed = new AppDbContext(_options))
        {
            seed.Categories.Add(TestDataBuilder.CreateCategory());
            var product = TestDataBuilder.CreateProduct(categoryId: 1);
            seed.Products.Add(product);
            await seed.SaveChangesAsync();
            seed.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                ImagePath = "/images/products/seed.jpg",
                DisplayOrder = 0,
                IsPrimary = true
            });
            await seed.SaveChangesAsync();
            productId = product.Id;
        }

        // Reproduce the controller's Edit POST in a single context:
        //   1) add new image, save
        //   2) UpdateAsync(formBoundProductWithEmptyImages)
        await using (var editContext = new AppDbContext(_options))
        {
            editContext.ProductImages.Add(new ProductImage
            {
                ProductId = productId,
                ImagePath = "/images/products/new.jpg",
                DisplayOrder = 1,
                IsPrimary = false
            });
            await editContext.SaveChangesAsync();

            var formBound = new Product
            {
                Id = productId,
                Name = "Edited",
                CostPrice = 1m,
                SellingPrice = 2m,
                CategoryId = 1,
                CurrentStock = 0,
                MinimumStock = 0
            };
            var repo = new ProductRepository(editContext);
            await repo.UpdateAsync(formBound);
        }

        await using (var verify = new AppDbContext(_options))
        {
            var images = await verify.ProductImages
                .Where(pi => pi.ProductId == productId)
                .OrderBy(pi => pi.DisplayOrder)
                .ToListAsync();

            Assert.Equal(2, images.Count);
            Assert.Equal("/images/products/seed.jpg", images[0].ImagePath);
            Assert.Equal("/images/products/new.jpg", images[1].ImagePath);
        }
    }

    [Fact]
    public async Task UpdateAsync_FormBoundProductWithEmptyImagesCollection_DoesNotDeleteExistingImages()
    {
        // Seed phase
        int productId;
        await using (var seed = new AppDbContext(_options))
        {
            var category = TestDataBuilder.CreateCategory();
            seed.Categories.Add(category);
            var product = TestDataBuilder.CreateProduct(categoryId: category.Id);
            seed.Products.Add(product);
            await seed.SaveChangesAsync();

            seed.ProductImages.Add(new ProductImage
            {
                ProductId = product.Id,
                ImagePath = "/images/products/seed.jpg",
                DisplayOrder = 0,
                IsPrimary = true
            });
            await seed.SaveChangesAsync();
            productId = product.Id;
        }

        // Edit POST phase — fresh context, form-bound product (no Images)
        await using (var editContext = new AppDbContext(_options))
        {
            var formBound = new Product
            {
                Id = productId,
                Name = "Edited",
                CostPrice = 1m,
                SellingPrice = 2m,
                CategoryId = 1,
                CurrentStock = 0,
                MinimumStock = 0
                // Images intentionally left empty — that's what form binding produces.
            };

            var repo = new ProductRepository(editContext);
            await repo.UpdateAsync(formBound);
        }

        // Verify phase
        await using (var verify = new AppDbContext(_options))
        {
            var imagesAfter = await verify.ProductImages
                .Where(pi => pi.ProductId == productId)
                .ToListAsync();

            Assert.NotEmpty(imagesAfter);
        }
    }
}
