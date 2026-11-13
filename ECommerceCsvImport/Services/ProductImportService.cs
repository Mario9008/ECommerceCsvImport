using System.Globalization;
using ECommerceCsvImport.Models;

namespace ECommerceCsvImport.Services;

public class ProductImportService : IProductImportService
{
    private readonly IProductRepository _repository;

    private static readonly string[] RequiredColumns =
    {
        "sku", "name", "description", "price", "currency", "category_name",
        "stock", "image_url", "is_active", "brand", "warranty_months", "model_number"
    };

    private const string ErrorFileNotFound = "Файл не найден";
    private const string ErrorNoData = "Данные для импорта отсутствуют";
    private const string ErrorBadHeaders = "Неверные заголовки CSV";
    private const string ErrorWrongFieldCount = "Неверное количество полей в строке";
    private const string ErrorDuplicateSku = "Артикул товара уже существует";
    private const string ErrorEmptySku = "Поле sku не может быть пустым";
    private const string ErrorEmptyName = "Поле name не может быть пустым";
    private const string ErrorBadPrice = "Поле price должно быть числом больше 0";
    private const string ErrorBadStock = "Остаток на складе должен быть целым числом не меньше 0";
    private const string ErrorBadActive = "Поле is_active должно иметь значение true, false, 1 или 0";
    private const string ErrorEmptyBrand = "Поле brand обязательно для варианта «Электроника»";
    private const string ErrorBadWarranty = "Гарантия должна быть целым числом от 0 до 60 месяцев";

    private const string DefaultCurrency = "RUB";
    private const string DefaultCategory = "Без категории";
    private const int MinWarrantyMonths = 0;
    private const int MaxWarrantyMonths = 60;

    public ProductImportService(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }


    public ImportResult ImportFromCsv(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return BuildSingleError(ErrorFileNotFound);

        if (!File.Exists(filePath))
            return BuildSingleError(ErrorFileNotFound);

        try
        {
            var lines = File.ReadAllLines(filePath);
            return ImportFromLines(lines);
        }
        catch (IOException)
        {
            return BuildSingleError("Ошибка чтения файла");
        }
        catch (UnauthorizedAccessException)
        {
            return BuildSingleError("Ошибка чтения файла");
        }
    }


    public ImportResult ImportFromLines(IEnumerable<string> lines)
    {
        var result = new ImportResult();
        var lineList = lines?.ToList() ?? new List<string>();

        if (lineList.Count == 0 || lineList.All(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = ErrorNoData });
            return result;
        }

        int headerIndex = -1;
        for (int i = 0; i < lineList.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lineList[i]))
            {
                headerIndex = i;
                break;
            }
        }

        int headerRowNumber = headerIndex + 1;

        var headerFields = CsvLineParser.Parse(lineList[headerIndex]);
        var columnMap = BuildColumnMap(headerFields);

        var missingColumns = RequiredColumns.Where(col => !columnMap.ContainsKey(col)).ToList();
        if (missingColumns.Count > 0)
        {
            result.Errors.Add(new ImportError { RowNumber = headerRowNumber, Message = ErrorBadHeaders });
            return result;
        }

        int expectedFieldCount = headerFields.Count;
        var importedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = headerIndex + 1; i < lineList.Count; i++)
        {
            var line = lineList[i];
            if (string.IsNullOrWhiteSpace(line))
                continue; 

            result.TotalRows++;
            int rowNumber = i + 1;

            var values = CsvLineParser.Parse(line);
            if (values.Count != expectedFieldCount)
            {
                result.Errors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorWrongFieldCount });
                continue;
            }

            string GetValue(string columnName)
            {
                if (columnMap.TryGetValue(columnName, out int idx) && idx < values.Count)
                    return values[idx];
                return string.Empty;
            }

            var rowErrors = new List<ImportError>();

            string sku = GetValue("sku").Trim();
            if (string.IsNullOrWhiteSpace(sku))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorEmptySku });

            string name = GetValue("name").Trim();
            if (string.IsNullOrWhiteSpace(name))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorEmptyName });

            bool priceValid = TryParseDecimal(GetValue("price"), out decimal price) && price > 0;
            if (!priceValid)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorBadPrice });

            bool stockValid = TryParseInt(GetValue("stock"), out int stock) && stock >= 0;
            if (!stockValid)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorBadStock });

            bool activeValid = TryParseBool(GetValue("is_active"), out bool isActive);
            if (!activeValid)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorBadActive });

            string brand = GetValue("brand").Trim();
            if (string.IsNullOrWhiteSpace(brand))
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorEmptyBrand });

            bool warrantyValid = TryParseInt(GetValue("warranty_months"), out int warrantyMonths)
                && warrantyMonths >= MinWarrantyMonths
                && warrantyMonths <= MaxWarrantyMonths;
            if (!warrantyValid)
                rowErrors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorBadWarranty });

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors);
                continue;
            }

            bool isDuplicate = _repository.GetBySku(sku) != null || importedSkus.Contains(sku);
            if (isDuplicate)
            {
                result.Errors.Add(new ImportError { RowNumber = rowNumber, Message = ErrorDuplicateSku });
                continue;
            }

            var product = new Product
            {
                Sku = sku,
                Name = name,
                Description = GetValue("description").Trim(),
                Price = price,
                Currency = NormalizeCurrency(GetValue("currency")),
                CategoryName = NormalizeCategory(GetValue("category_name")),
                StockQuantity = stock,
                ImageUrl = GetValue("image_url").Trim(),
                IsActive = isActive,
                Brand = brand,
                WarrantyMonths = warrantyMonths,
                ModelNumber = GetValue("model_number").Trim()
            };

            _repository.Add(product);
            importedSkus.Add(sku);
            result.ImportedCount++;
        }

        if (result.TotalRows == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Message = ErrorNoData });
        }

        return result;
    }


    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headerFields)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerFields.Count; i++)
        {
            var columnName = headerFields[i].Trim();
            if (!map.ContainsKey(columnName))
                map[columnName] = i; 
        }
        return map;
    }

    private static ImportResult BuildSingleError(string message)
    {
        var result = new ImportResult();
        result.Errors.Add(new ImportError { RowNumber = 0, Message = message });
        return result;
    }

    private static bool TryParseDecimal(string value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool TryParseInt(string value, out int result)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseBool(string value, out bool result)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed == "1" || string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }
        if (trimmed == "0" || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }
        result = false;
        return false;
    }

    private static string NormalizeCurrency(string value)
        => string.IsNullOrWhiteSpace(value) ? DefaultCurrency : value.Trim().ToUpperInvariant();

    private static string NormalizeCategory(string value)
        => string.IsNullOrWhiteSpace(value) ? DefaultCategory : value.Trim();
}