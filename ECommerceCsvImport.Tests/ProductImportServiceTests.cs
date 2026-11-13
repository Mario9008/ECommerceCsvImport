using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ECommerceCsvImport.Tests;

public class ProductImportServiceTests
{
    private const string Header = "sku,name,description,price,currency,category_name,stock,image_url,is_active,brand,warranty_months,model_number";

    private static (ProductImportService service, InMemoryProductRepository repo) CreateService()
    {
        var repo = new InMemoryProductRepository();
        var service = new ProductImportService(repo);
        return (service, repo);
    }

    [Fact]
    public void Import_SuccessfulSingleRow()
    {
        var (service, repo) = CreateService();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15"
        };

        var result = service.ImportFromLines(lines);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);

        var product = repo.GetBySku("1001");
        Assert.NotNull(product);
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal(45990m, product.Price);
        Assert.Equal("Lenovo", product.Brand);
        Assert.Equal(24, product.WarrantyMonths);
        Assert.Equal("IdeaPad 3-15", product.ModelNumber);
    }

    [Fact]
    public void Import_SuccessfulMultipleRows()
    {
        var (service, repo) = CreateService();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук,,45990,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad",
            "1002,Мышь,,1290,RUB,Периферия,10,,true,Logitech,12,M185"
        };

        var result = service.ImportFromLines(lines);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.Errors);
        Assert.Equal(2, repo.Count);
    }

    [Fact]
    public void ImportFromCsv_FileNotFound_ReturnsError()
    {
        var (service, _) = CreateService();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");

        var result = service.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Файл не найден", result.Errors[0].Message);
    }

    [Fact]
    public void ImportFromCsv_EmptyFile_ReturnsError()
    {
        var (service, _) = CreateService();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".csv");
        File.WriteAllText(path, string.Empty);

        var result = service.ImportFromCsv(path);

        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);

        File.Delete(path); 
    }

    [Fact]
    public void Import_OnlyHeader_ReturnsNoDataError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header };

        var result = service.ImportFromLines(lines);

        Assert.Equal(0, result.TotalRows);
        Assert.Single(result.Errors);
        Assert.Contains("Данные для импорта отсутствуют", result.Errors[0].Message);
    }

    [Fact]
    public void Import_BadHeaders_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { "sku,name,description,price,currency" }; 

        var result = service.ImportFromLines(lines);

        Assert.Single(result.Errors);
        Assert.Contains("Неверные заголовки CSV", result.Errors[0].Message);
    }

    [Fact]
    public void Import_WrongFieldCount_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук" }; 

        var result = service.ImportFromLines(lines);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Неверное количество полей в строке", result.Errors[0].Message);
    }

    [Fact]
    public void Import_EmptySku_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, ",Ноутбук,,45990,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Errors, e => e.Message.Contains("Поле sku не может быть пустым"));
    }

    [Fact]
    public void Import_EmptyName_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,,,45990,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Поле name не может быть пустым"));
    }

    [Fact]
    public void Import_NegativePrice_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,, -500,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Поле price должно быть числом больше 0"));
    }

    [Fact]
    public void Import_NonNumericPrice_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,abc,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Поле price должно быть числом больше 0"));
    }

    [Fact]
    public void Import_NegativeStock_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,45990,RUB,Компьютеры,-5,,true,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Остаток на складе должен быть целым числом не меньше 0"));
    }

    [Fact]
    public void Import_InvalidIsActive_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,45990,RUB,Компьютеры,5,,yes,Lenovo,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Поле is_active должно иметь значение true, false, 1 или 0"));
    }

    [Fact]
    public void Import_MissingBrand_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,45990,RUB,Компьютеры,5,,true,,24,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Поле brand обязательно для варианта «Электроника»"));
    }

    [Fact]
    public void Import_WarrantyLessThanZero_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,45990,RUB,Компьютеры,5,,true,Lenovo,-1,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Гарантия должна быть целым числом от 0 до 60 месяцев"));
    }

    [Fact]
    public void Import_WarrantyMoreThan60_ReturnsError()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук,,45990,RUB,Компьютеры,5,,true,Lenovo,61,IdeaPad" };

        var result = service.ImportFromLines(lines);

        Assert.Contains(result.Errors, e => e.Message.Contains("Гарантия должна быть целым числом от 0 до 60 месяцев"));
    }

    [Fact]
    public void Import_DuplicateSkuInsideFile_ReturnsError()
    {
        var (service, repo) = CreateService();
        var lines = new[]
        {
            Header,
            "1001,Ноутбук Lenovo,,45990,RUB,Компьютеры,5,,true,Lenovo,24,IdeaPad",
            "1001,Ноутбук ASUS,,52990,RUB,Компьютеры,7,,true,ASUS,24,VivoBook"
        };

        var result = service.ImportFromLines(lines);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.Errors);
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);

        var product = repo.GetBySku("1001");
        Assert.Equal("Ноутбук Lenovo", product.Name);
    }

    [Fact]
    public void Import_DuplicateSkuInRepository_ReturnsError()
    {
        var (service, repo) = CreateService();
        repo.Add(new Product { Sku = "1001", Name = "Старый товар" });

        var lines = new[] { Header, "1001,Ноутбук ASUS,,52990,RUB,Компьютеры,7,,true,ASUS,24,VivoBook" };

        var result = service.ImportFromLines(lines);

        Assert.Equal(1, repo.Count); 
        Assert.Contains("Артикул товара уже существует", result.Errors[0].Message);
    }

    [Fact]
    public void Import_MultipleErrorsInOneRow_ReturnsAllErrors()
    {
        var (service, _) = CreateService();
        var lines = new[] { Header, ",Некорректная строка,, -10,RUB,Компьютеры,-5,/images/x.jpg,yes,,61," };

        var result = service.ImportFromLines(lines);

        Assert.Equal(0, result.ImportedCount);
        Assert.True(result.Errors.Count >= 4);
    }

    [Fact]
    public void Import_EmptyOptionalFields_IsValid()
    {
        var (service, repo) = CreateService();
        var lines = new[] { Header, "1011,Блок питания Corsair CV550,,4990,RUB,Компьютеры,9,,true,Corsair,60,CV550" };

        var result = service.ImportFromLines(lines);

        Assert.Equal(1, result.ImportedCount);
        Assert.Empty(result.Errors);

        var product = repo.GetBySku("1011");
        Assert.Equal(string.Empty, product.Description);
        Assert.Equal(string.Empty, product.ImageUrl);
    }

    [Fact]
    public void Import_EmptyCurrency_DefaultsToRub()
    {
        var (service, repo) = CreateService();
        var lines = new[] { Header, "1011,Блок питания,,4990,,Компьютеры,9,,true,Corsair,60,CV550" };

        var result = service.ImportFromLines(lines);

        var product = repo.GetBySku("1011");
        Assert.Equal("RUB", product.Currency);
    }

    [Fact]
    public void Import_EmptyCategory_DefaultsToBezKategorii()
    {
        var (service, repo) = CreateService();
        var lines = new[] { Header, "1011,Блок питания,,4990,RUB,,9,,true,Corsair,60,CV550" };

        var result = service.ImportFromLines(lines);

        var product = repo.GetBySku("1011");
        Assert.Equal("Без категории", product.CategoryName);
    }

    [Fact]
    public void Import_CyrillicIsPreserved()
    {
        var (service, repo) = CreateService();
        var lines = new[] { Header, "1001,Ноутбук Lenovo IdeaPad 3,Бюджетный ноутбук для работы и учебы,45990,RUB,Компьютеры,5,/images/laptop.jpg,true,Lenovo,24,IdeaPad 3-15" };

        var result = service.ImportFromLines(lines);

        var product = repo.GetBySku("1001");
        Assert.Equal("Ноутбук Lenovo IdeaPad 3", product.Name);
        Assert.Equal("Бюджетный ноутбук для работы и учебы", product.Description);
        Assert.Equal("Компьютеры", product.CategoryName);
    }
}