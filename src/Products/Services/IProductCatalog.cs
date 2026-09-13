using Products.Models;

namespace Products.Services;

/// <summary>Read-only access to the product catalogue.</summary>
public interface IProductCatalog
{
    /// <summary>Returns every product in the catalogue.</summary>
    IReadOnlyList<Product> GetAll();
}
