using System.IO;
using System.Linq;
using ECommerceCsvImport.Models;
using ECommerceCsvImport.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceCsvImport.Controllers;

public class ProductsController : Controller
{
    private readonly IProductRepository _repository;
    private readonly IProductImportService _importService;
    private readonly IWebHostEnvironment _environment;

    public ProductsController(
        IProductRepository repository,
        IProductImportService importService,
        IWebHostEnvironment environment)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _importService = importService ?? throw new ArgumentNullException(nameof(importService));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }
    [HttpGet]
    public IActionResult Index(string? searchString)
    {
        var products = _repository.GetAll();

        if (!string.IsNullOrWhiteSpace(searchString))
        {
            products = products.Where(p =>
                p.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                p.Sku.Contains(searchString, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        var viewModel = new ProductsIndexViewModel
        {
            Products = products,
            SearchString = searchString ?? string.Empty
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Import(ImportViewModel model)
    {
        try
        {
            if (model.UploadedFile != null && model.UploadedFile.Length > 0)
            {
                using var reader = new StreamReader(model.UploadedFile.OpenReadStream());
                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }

                model.Result = _importService.ImportFromLines(lines);
                return View("Import", model);
            }

            if (!string.IsNullOrWhiteSpace(model.FilePath))
            {
                var resolvedPath = ResolveFilePath(model.FilePath);
                model.Result = _importService.ImportFromCsv(resolvedPath);
                return View("Import", model);
            }

            model.Result = new ImportResult
            {
                Errors = { new ImportError { RowNumber = 0, Message = "Выберите файл или укажите путь к CSV" } }
            };
            return View("Import", model);
        }
        catch (Exception ex)
        {
            model.Result = new ImportResult
            {
                Errors = { new ImportError
                {
                    RowNumber = 0,
                    Message = $"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}. Подробности: {ex.InnerException?.Message}"
                } }
            };
            return View("Import", model);
        }
    }

    private string ResolveFilePath(string filePath)
    {
        var trimmed = filePath.Trim().Replace('/', Path.DirectorySeparatorChar);

        if (trimmed.StartsWith(Path.DirectorySeparatorChar))
        {
            var relative = trimmed.TrimStart(Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath ?? string.Empty, relative);
        }

        if (Path.IsPathRooted(trimmed))
            return trimmed;

        return Path.Combine(_environment.ContentRootPath, trimmed);
    }
}