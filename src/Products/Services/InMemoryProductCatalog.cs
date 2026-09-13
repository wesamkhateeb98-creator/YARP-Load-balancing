using Products.Models;

namespace Products.Services;

/// <summary>
/// Deterministic in-memory catalogue. Every instance serves the identical payload so that
/// any variation observed through the gateway comes from routing, never from the data.
/// </summary>
public sealed class InMemoryProductCatalog : IProductCatalog
{
    private static readonly IReadOnlyList<Product> Catalogue =
    [
        new(1, "Mechanical Keyboard", "Peripherals", 129.99m, 42),
        new(2, "27\" 4K Monitor", "Displays", 379.00m, 18),
        new(3, "USB-C Docking Station", "Accessories", 189.50m, 7),
        new(4, "Noise Cancelling Headset", "Audio", 249.95m, 31),
        new(5, "Ergonomic Vertical Mouse", "Peripherals", 64.25m, 56),
        new(6, "1TB NVMe SSD", "Storage", 112.40m, 23)
    ];

    /// <inheritdoc />
    public IReadOnlyList<Product> GetAll() => Catalogue;
}
