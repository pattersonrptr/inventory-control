# Plan — v6.1.0: Enriched Models, Multiple Images, Inline Creation & Seed Data

**Version**: 6.1.0 (MINOR — new features, fully backwards-compatible)
**Branch**: `feat/enriched-models-v6.1.0`
**Status**: Planning

---

## Context

The sister's product spreadsheet (`planilha_caramelo_fofa.xlsx`) revealed gaps in our model compared to real-world stationery store needs. She tracks SKU (already supported but not exposed in forms), Brand, Subcategory, and supplier contact/lead time info that our system lacks.

Additionally, two UX features were requested:
- **Multiple images per product** (currently single image)
- **Inline creation of categories/suppliers** from the product form (currently requires navigating away)

This version adds all of the above plus development seed data based on her real product catalog.

---

## 1. New Model Fields

### 1.1 Product — `Brand` field

| Item | Detail |
|------|--------|
| Property | `string? Brand` — `[StringLength(100)]` |
| DB | Nullable VARCHAR(100), indexed for filtering |
| Views | Add to Create, Edit, Details, Index (new column) |
| CSV Import | Map to Brand column |
| Impact | Low — nullable field, no logic changes |

### 1.2 Category — `ParentId` (hierarchical subcategories)

| Item | Detail |
|------|--------|
| Property | `int? ParentId` — self-referencing FK to `Category.Id` |
| Navigation | `Category? Parent` + `ICollection<Category> Children` |
| DB | Nullable FK with `ON DELETE RESTRICT` (prevent deleting parent with children) |
| Display | Full path: "ESCRITA > CANETAS" in product dropdowns and listings |
| Views | Categories/Create and Edit: dropdown "Categoria Pai" (optional). Index: show hierarchy with indentation or "Pai > Filha" display |
| Validation | Prevent circular references (a category cannot be its own ancestor) |
| Impact | Medium — new FK, dropdown logic, display formatting, cycle prevention |

### 1.3 Supplier — `ContactName`, `LeadTimeDays`, `Notes`

| Property | Type | Detail |
|----------|------|--------|
| `ContactName` | `string?` `[StringLength(200)]` | Contact person name |
| `LeadTimeDays` | `int?` `[Range(0, 365)]` | Delivery time in days |
| `Notes` | `string?` `[StringLength(1000)]` | Free-text observations |

| Item | Detail |
|------|--------|
| DB | 3 nullable columns |
| Views | Add to Create, Edit, Index (ContactName column), Details |
| CSV Import | Map ContactName, LeadTimeDays, Notes |
| Impact | Low — 3 nullable fields, no logic changes |

### 1.4 SKU exposure in forms

SKU (`Product.Sku`) already exists in the model since v3.1.0 with a unique filtered index, and is present in Create/Edit views. **No model changes needed** — just verify it works end-to-end with the seed data.

---

## 2. Multiple Images per Product

### 2.1 New `ProductImage` model

```csharp
public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, StringLength(500)]
    public string ImagePath { get; set; } = string.Empty;

    [StringLength(200)]
    public string? AltText { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
}
```

### 2.2 Migration plan

- New `ProductImages` table with FK to `Products`
- Migrate existing `Product.ImagePath` data: for each product with a non-null `ImagePath`, create a `ProductImage` record with `IsPrimary = true`
- Keep `Product.ImagePath` as a computed/convenience property (not mapped) that returns the primary image, OR remove it and always query through `ProductImages`

**Decision**: Remove `Product.ImagePath` column after data migration. Add a `[NotMapped]` convenience property `PrimaryImagePath` that reads from the `Images` collection.

### 2.3 Views changes

| View | Change |
|------|--------|
| Products/Create | Multi-file `<input type="file" multiple>`, drag-and-drop area |
| Products/Edit | Show existing images as thumbnails with delete (X) button, add new images, drag to reorder |
| Products/Details | Image gallery/carousel (Bootstrap carousel or simple grid) |
| Products/Index | Show primary image thumbnail in table (small) |

### 2.4 Controller changes

- `Create POST`: accept `IFormFile[] images`, save each to `wwwroot/images/products/`, create `ProductImage` records
- `Edit POST`: handle image additions, deletions (by ID), and primary selection
- `Delete POST`: delete associated image files from disk
- New endpoint: `POST /Products/DeleteImage/{id}` — AJAX delete of single image (returns JSON)

### 2.5 Impact

**Medium-High** — New table, migration with data transfer, view redesign, controller logic for multi-file upload/delete/reorder.

---

## 3. Inline Category/Supplier Creation (Modal)

### 3.1 Architecture

When creating/editing a product, the user can click a "+" button next to the Category or Supplier dropdown. This opens a Bootstrap modal with a mini-form. On submit, an AJAX POST creates the entity and adds it to the dropdown without page reload.

### 3.2 New API endpoints

```
POST /Categories/CreateInline  → returns JSON { id, name, fullName }
POST /Suppliers/CreateInline   → returns JSON { id, name }
```

Both endpoints:
- Accept JSON body (not form-encoded) — `[FromBody]`
- Return `201 Created` with the new entity or `400 Bad Request` with validation errors
- Have `[ValidateAntiForgeryToken]` (token sent via AJAX header)
- Require authentication (already global)

### 3.3 Views changes

| View | Change |
|------|--------|
| Products/Create | "+" button next to Category and Supplier dropdowns, modal partial includes |
| Products/Edit | Same as Create |
| Shared/_CategoryModal.cshtml | New partial — Name, Description, ParentId dropdown |
| Shared/_SupplierModal.cshtml | New partial — Name, ContactName, Phone, Email |

### 3.4 JavaScript

- `wwwroot/js/inline-create.js` — handles modal open, AJAX POST, append new `<option>` to `<select>`, auto-select it, close modal, show toast on success/error

### 3.5 Impact

**Medium** — New endpoints, partials, JS file. No DB changes beyond what's in section 1.

---

## 4. Seed Data (Development Only)

### 4.1 Strategy

- Seed runs only in `Development` environment
- Runs after migrations, before app starts
- Skips if data already exists (check `Categories.Any()`)
- Based on the sister's real spreadsheet (~20 products, real SKUs and brands)

### 4.2 Data set

**Categories (hierarchical):**
| Name | Parent |
|------|--------|
| ESCRITA | — |
| CANETAS | ESCRITA |
| ACESSÓRIOS | — |
| PAPELARIA | — |

**Suppliers:**
| Name | ContactName | Notes |
|------|-------------|-------|
| ALIEXPRESS | — | Importação direta, prazo ~30 dias |
| STAR ATACADO | — | Atacado papelaria |
| CANETAS ATACADO | — | Especializado em canetas |
| TEMU | — | Importação, prazo variável |
| DESCONTO AQUI | — | — |
| PAPELERA ATACADO | — | — |

**Brands** (as Product.Brand values):
IMPORTADA, BRW, MOLIN, CIS, TRIS, TILIBRA, FOFY, LEONORA, COMPACTOR

**Products** (~20 sample, from spreadsheet):
- Canetas gel, esferográficas, apagáveis
- With real SKUs (e.g., `001-CG-DOGN-ARV`)
- With brand, category (CANETAS), supplier
- Prices in R$ (comma decimal in the CSV, dot in DB)
- Stock levels (0–5 typically)

### 4.3 Implementation

- New method `SeedDevelopmentDataAsync(IServiceProvider)` in `Program.cs`
- Called after `app.MapControllerRoute()` inside `if (app.Environment.IsDevelopment())`

### 4.4 Impact

**Low** — Development only, no production impact.

---

## 5. File-by-File Change Map

### Models (3 files modified, 1 new)

| File | Change |
|------|--------|
| `Models/Product.cs` | Add `Brand`, remove `ImagePath` (→ `[NotMapped] PrimaryImagePath`), add `ICollection<ProductImage> Images` |
| `Models/Category.cs` | Add `ParentId`, `Parent`, `Children` |
| `Models/Supplier.cs` | Add `ContactName`, `LeadTimeDays`, `Notes` |
| `Models/ProductImage.cs` | **NEW** — Id, ProductId, ImagePath, AltText, DisplayOrder, IsPrimary |

### Data (1 file modified)

| File | Change |
|------|--------|
| `Data/AppDbContext.cs` | Configure `ProductImage`, `Category.ParentId` FK, `Product.Brand` index, Supplier new columns |

### Migration (1 new)

| File | Change |
|------|--------|
| `Migrations/YYYYMMDD_EnrichedModels.cs` | **NEW** — AddColumn Brand, ParentId, ContactName, LeadTimeDays, Notes; CreateTable ProductImages; MigrateData ImagePath→ProductImages; DropColumn ImagePath |

### Controllers (3 files modified)

| File | Change |
|------|--------|
| `Controllers/ProductsController.cs` | Multi-image upload/delete, update `SaveImageAsync` → `SaveImagesAsync`, update dropdowns to show category hierarchy |
| `Controllers/CategoriesController.cs` | Add `CreateInline` POST, add parent dropdown in Create/Edit, cycle validation |
| `Controllers/SuppliersController.cs` | Add `CreateInline` POST |

### Views (12+ files modified, 3 new)

| File | Change |
|------|--------|
| `Views/Products/Create.cshtml` | Multi-image upload, Brand field, "+" buttons for inline create |
| `Views/Products/Edit.cshtml` | Image gallery management, Brand field, "+" buttons |
| `Views/Products/Details.cshtml` | Image carousel/gallery, Brand display |
| `Views/Products/Index.cshtml` | Thumbnail column, Brand column |
| `Views/Categories/Create.cshtml` | ParentId dropdown |
| `Views/Categories/Edit.cshtml` | ParentId dropdown |
| `Views/Categories/Index.cshtml` | Show hierarchy (Parent > Child), indent children |
| `Views/Suppliers/Create.cshtml` | ContactName, LeadTimeDays, Notes fields |
| `Views/Suppliers/Edit.cshtml` | ContactName, LeadTimeDays, Notes fields |
| `Views/Suppliers/Index.cshtml` | ContactName column |
| `Views/Shared/_CategoryModal.cshtml` | **NEW** — Modal for inline category creation |
| `Views/Shared/_SupplierModal.cshtml` | **NEW** — Modal for inline supplier creation |

### JavaScript (1 new)

| File | Change |
|------|--------|
| `wwwroot/js/inline-create.js` | **NEW** — AJAX handlers for modal create |

### Startup (1 file modified)

| File | Change |
|------|--------|
| `Program.cs` | Add `SeedDevelopmentDataAsync()`, update PostgreSQL sequence fix for `ProductImages` table |

### Tests (4+ files modified)

| File | Change |
|------|--------|
| `Integration/FormSubmissionTests.cs` | Update seed helpers for new fields, add tests for Brand, ParentId, inline creation endpoints |
| `Unit/Models/ProductTests.cs` | Add tests for `PrimaryImagePath` computed property |
| `Unit/Repositories/CategoryRepositoryTests.cs` | Add tests for hierarchical queries |
| `Unit/Repositories/ProductRepositoryTests.cs` | Update for Brand field |

### Documentation (2 files modified)

| File | Change |
|------|--------|
| `CHANGELOG.md` | New entries under `[Unreleased]` |
| `README.md` | Update features list, model diagram if any |

---

## 6. Implementation Order

| Step | Task | Depends On |
|------|------|------------|
| 1 | Models: add fields to `Product`, `Category`, `Supplier`; create `ProductImage` | — |
| 2 | `AppDbContext`: configure new entities and relationships | Step 1 |
| 3 | EF Core migration | Step 2 |
| 4 | Hierarchical categories: controller + views (ParentId dropdown, display) | Step 3 |
| 5 | Supplier new fields: views (Create, Edit, Index) | Step 3 |
| 6 | Product Brand field: views (Create, Edit, Details, Index) | Step 3 |
| 7 | Multiple images: controller (`SaveImagesAsync`, delete endpoint), views (upload, gallery, carousel) | Step 3 |
| 8 | Inline creation: API endpoints, modal partials, JavaScript | Steps 4–5 |
| 9 | Seed data | Step 3 |
| 10 | Update tests | Steps 4–8 |
| 11 | Update CHANGELOG, README | Step 10 |
| 12 | Run full test suite, verify | Step 11 |

---

## 7. Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Category circular reference (A→B→A) | Medium | Validate in controller before save: walk ParentId chain, reject if cycle |
| ImagePath migration data loss | Low | Migration copies data before dropping column; test on dev DB first |
| InMemory DB doesn't support new FK constraints | Low | Already using `ConfigureWarnings` to suppress; self-ref FK works in InMemory |
| Multi-file upload size limits | Low | Keep existing per-file validation; add total upload size check |
| Existing CSV import breaks | Low | New fields are nullable; old CSVs still work, new columns optional |
| Antiforgery token in AJAX modals | Medium | Pass token via `X-RequestVerificationToken` header from `@Html.AntiForgeryToken()` |

---

## 8. Out of Scope (Future Versions)

- Drag-and-drop image reorder (can use DisplayOrder manually for now)
- Bulk product import from the sister's spreadsheet (CSV import already exists, just needs updated column mapping)
- Category tree view with expand/collapse
- Product variants (size, color as separate entities)
- Barcode/QR code generation from SKU
