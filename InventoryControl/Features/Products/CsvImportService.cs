using System.Globalization;

namespace InventoryControl.Features.Products;

public class CsvImportResult<T>
{
    public List<T> ValidItems { get; set; } = [];
    public List<CsvImportError> Errors { get; set; } = [];
    public int TotalRows { get; set; }
    public string[] Headers { get; set; } = [];
}

public class CsvImportError
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}

public static class CsvImportService
{
    private static readonly char[] Separators = [',', ';'];

    public static CsvImportResult<Product> ParseProducts(Stream stream, IEnumerable<Category> categories)
    {
        var result = new CsvImportResult<Product>();
        var categoryLookup = categories.ToDictionary(c => c.Name.Trim(), c => c.Id, StringComparer.OrdinalIgnoreCase);

        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "CSV file is empty or missing header row." });
            return result;
        }

        var separator = DetectSeparator(headerLine);
        result.Headers = ParseCsvLine(headerLine, separator);
        var headers = result.Headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();

        var nameIdx = Array.FindIndex(headers, h => h is "name" or "nome");
        var costIdx = Array.FindIndex(headers, h => h is "costprice" or "preço de custo" or "preco de custo" or "custo");
        var sellIdx = Array.FindIndex(headers, h => h is "sellingprice" or "preço de venda" or "preco de venda" or "venda");
        var minIdx = Array.FindIndex(headers, h => h is "minimumstock" or "estoque mínimo" or "estoque minimo" or "mínimo" or "minimo");
        var skuIdx = Array.FindIndex(headers, h => h is "sku");
        var descIdx = Array.FindIndex(headers, h => h is "description" or "descrição" or "descricao");
        var catIdx = Array.FindIndex(headers, h => h is "category" or "categoria");

        if (nameIdx < 0)
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "Required column 'Name' (or 'Nome') not found in header." });
            return result;
        }

        int row = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            row++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.TotalRows++;

            var cols = ParseCsvLine(line, separator);

            var name = GetCol(cols, nameIdx);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new CsvImportError { Row = row, Message = "Name is required." });
                continue;
            }

            var product = new Product { Name = name.Trim() };

            if (descIdx >= 0) product.Description = GetCol(cols, descIdx);
            if (skuIdx >= 0) product.Sku = GetCol(cols, skuIdx);

            if (costIdx >= 0 && TryParseDecimal(GetCol(cols, costIdx), out var cost))
                product.CostPrice = cost;
            else if (costIdx >= 0)
            {
                result.Errors.Add(new CsvImportError { Row = row, Message = $"Invalid cost price: '{GetCol(cols, costIdx)}'." });
                continue;
            }

            if (sellIdx >= 0 && TryParseDecimal(GetCol(cols, sellIdx), out var sell))
                product.SellingPrice = sell;
            else if (sellIdx >= 0)
            {
                result.Errors.Add(new CsvImportError { Row = row, Message = $"Invalid selling price: '{GetCol(cols, sellIdx)}'." });
                continue;
            }

            if (minIdx >= 0 && int.TryParse(GetCol(cols, minIdx)?.Trim(), out var min))
                product.MinimumStock = min;

            if (catIdx >= 0)
            {
                var catName = GetCol(cols, catIdx)?.Trim();
                if (!string.IsNullOrWhiteSpace(catName))
                {
                    if (categoryLookup.TryGetValue(catName, out var catId))
                        product.CategoryId = catId;
                    else
                    {
                        result.Errors.Add(new CsvImportError { Row = row, Message = $"Category '{catName}' not found." });
                        continue;
                    }
                }
            }

            result.ValidItems.Add(product);
        }

        return result;
    }

    public static CsvImportResult<Category> ParseCategories(Stream stream)
    {
        var result = new CsvImportResult<Category>();

        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "CSV file is empty or missing header row." });
            return result;
        }

        var separator = DetectSeparator(headerLine);
        result.Headers = ParseCsvLine(headerLine, separator);
        var headers = result.Headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();

        var nameIdx = Array.FindIndex(headers, h => h is "name" or "nome");
        var descIdx = Array.FindIndex(headers, h => h is "description" or "descrição" or "descricao");

        if (nameIdx < 0)
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "Required column 'Name' (or 'Nome') not found in header." });
            return result;
        }

        int row = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            row++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.TotalRows++;

            var cols = ParseCsvLine(line, separator);
            var name = GetCol(cols, nameIdx);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new CsvImportError { Row = row, Message = "Name is required." });
                continue;
            }

            var category = new Category { Name = name.Trim() };
            if (descIdx >= 0) category.Description = GetCol(cols, descIdx);

            result.ValidItems.Add(category);
        }

        return result;
    }

    public static CsvImportResult<Supplier> ParseSuppliers(Stream stream)
    {
        var result = new CsvImportResult<Supplier>();

        using var reader = new StreamReader(stream);
        var headerLine = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "CSV file is empty or missing header row." });
            return result;
        }

        var separator = DetectSeparator(headerLine);
        result.Headers = ParseCsvLine(headerLine, separator);
        var headers = result.Headers.Select(h => h.Trim().ToLowerInvariant()).ToArray();

        var nameIdx = Array.FindIndex(headers, h => h is "name" or "nome");
        var cnpjIdx = Array.FindIndex(headers, h => h is "cnpj");
        var phoneIdx = Array.FindIndex(headers, h => h is "phone" or "telefone");
        var emailIdx = Array.FindIndex(headers, h => h is "email" or "e-mail");

        if (nameIdx < 0)
        {
            result.Errors.Add(new CsvImportError { Row = 0, Message = "Required column 'Name' (or 'Nome') not found in header." });
            return result;
        }

        int row = 1;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            row++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            result.TotalRows++;

            var cols = ParseCsvLine(line, separator);
            var name = GetCol(cols, nameIdx);
            if (string.IsNullOrWhiteSpace(name))
            {
                result.Errors.Add(new CsvImportError { Row = row, Message = "Name is required." });
                continue;
            }

            var supplier = new Supplier { Name = name.Trim() };
            if (cnpjIdx >= 0) supplier.Cnpj = GetCol(cols, cnpjIdx);
            if (phoneIdx >= 0) supplier.Phone = GetCol(cols, phoneIdx);
            if (emailIdx >= 0) supplier.Email = GetCol(cols, emailIdx);

            result.ValidItems.Add(supplier);
        }

        return result;
    }

    private static char DetectSeparator(string line)
        => line.Count(c => c == ';') >= line.Count(c => c == ',') ? ';' : ',';

    private static string[] ParseCsvLine(string line, char separator)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == separator && !inQuotes)
            {
                fields.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        fields.Add(current.ToString().Trim());
        return fields.ToArray();
    }

    private static string? GetCol(string[] cols, int index)
        => index >= 0 && index < cols.Length ? cols[index] : null;

    private static bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim();

        // Try pt-BR format first (1.234,56), then invariant (1,234.56)
        return decimal.TryParse(value, NumberStyles.Number, new CultureInfo("pt-BR"), out result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
