using Microsoft.AspNetCore.Http;

namespace ECommerceCsvImport.Models;

public class ImportViewModel
{
    public string FilePath { get; set; } = string.Empty;

    public IFormFile? UploadedFile { get; set; }

    public ImportResult? Result { get; set; }
}