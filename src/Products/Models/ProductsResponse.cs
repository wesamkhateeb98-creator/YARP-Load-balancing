namespace Products.Models;

/// <summary>
/// Envelope for <c>GET /api/products</c>: the catalogue plus the instance that produced it.
/// </summary>
public sealed record ProductsResponse(InstanceInfo ServedBy, IReadOnlyList<Product> Products)
{
    /// <summary>Number of products in <see cref="Products"/>.</summary>
    public int Count => Products.Count;
}
