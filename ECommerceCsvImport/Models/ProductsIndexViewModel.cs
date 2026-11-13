namespace ECommerceCsvImport.Models;

public class ProductsIndexViewModel
{
    public IReadOnlyList<Product> Products { get; set; } = new List<Product>();

    public string SearchString { get; set; } = string.Empty;

    public int TotalCount => Products.Count;
    public decimal TotalValue => Products.Sum(p => p.Price);
}