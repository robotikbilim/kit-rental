using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Warehouse;

public sealed class Component
{
    private Component()
    {
    }

    private Component(Guid id, string name, string sku, string unitOfMeasure, decimal minimumStock,
        string? imageUrl, Guid? defaultStorageLocationId)
    {
        Id = id;
        Name = name;
        Sku = sku;
        UnitOfMeasure = unitOfMeasure;
        MinimumStock = minimumStock;
        ImageUrl = imageUrl;
        DefaultStorageLocationId = defaultStorageLocationId;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string UnitOfMeasure { get; private set; } = string.Empty;
    public decimal MinimumStock { get; private set; }
    public string? ImageUrl { get; private set; }
    public Guid? DefaultStorageLocationId { get; private set; }
    public bool IsActive { get; private set; }

    public static Component Create(Guid id, string name, string sku, string unitOfMeasure, decimal minimumStock,
        string? imageUrl = null, Guid? defaultStorageLocationId = null)
    {
        if (id == Guid.Empty)
            throw new DomainException("component.id_required", "Komponent kimliği zorunludur.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(unitOfMeasure))
            throw new DomainException("component.fields_required", "Komponent adı, SKU ve ölçü birimi zorunludur.");
        if (minimumStock < 0)
            throw new DomainException("component.minimum_stock_invalid", "Minimum stok sıfırdan küçük olamaz.");
        var normalizedImageUrl = NormalizeImageUrl(imageUrl);

        return new Component(id, name.Trim(), sku.Trim().ToUpperInvariant(), unitOfMeasure.Trim(), minimumStock,
            normalizedImageUrl, defaultStorageLocationId);
    }

    public void Update(string name, string sku, string unitOfMeasure, decimal minimumStock, string? imageUrl,
        Guid? defaultStorageLocationId)
    {
        var updated = Create(Id, name, sku, unitOfMeasure, minimumStock, imageUrl, defaultStorageLocationId);
        Name = updated.Name;
        Sku = updated.Sku;
        UnitOfMeasure = updated.UnitOfMeasure;
        MinimumStock = updated.MinimumStock;
        ImageUrl = updated.ImageUrl;
        DefaultStorageLocationId = updated.DefaultStorageLocationId;
    }

    public void ClearDefaultStorageLocation() => DefaultStorageLocationId = null;

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;
        var value = imageUrl.Trim();
        if (value.Length > 1000)
            throw new DomainException("component.image_url_invalid", "Komponent görsel adresi çok uzun.");
        if (value.StartsWith('/'))
            return value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new DomainException("component.image_url_invalid", "Komponent görseli HTTP/HTTPS adresi veya uygulama içi yol olmalıdır.");
        return value;
    }
}
