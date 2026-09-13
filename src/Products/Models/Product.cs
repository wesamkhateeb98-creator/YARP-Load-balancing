namespace Products.Models;

/// <summary>
/// A single catalogue item returned by <c>GET /api/products</c>.
/// </summary>
/// <param name="Id">Stable catalogue identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Category">Merchandising category.</param>
/// <param name="Price">Unit price in USD.</param>
/// <param name="StockQuantity">Units currently available.</param>
public sealed record Product(
    int Id,
    string Name,
    string Category,
    decimal Price,
    int StockQuantity);
