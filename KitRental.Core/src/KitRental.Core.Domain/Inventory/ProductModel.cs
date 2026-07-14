using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Inventory;

public sealed class ProductModel
{
    private ProductModel()
    {
    }

    private ProductModel(Guid id, string name, string sku, string? description, string? imageUrl)
    {
        Id = id;
        Name = name;
        Sku = sku;
        Description = description;
        ImageUrl = imageUrl;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }

    public static ProductModel Create(Guid id, string name, string sku, string? description = null, string? imageUrl = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("product_model.id_required", "Ürün modeli kimliği boş olamaz.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("product_model.name_required", "Ürün modeli adı zorunludur.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("product_model.sku_required", "SKU zorunludur.");
        }

        var normalizedImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        if (normalizedImageUrl?.Length > 1000)
            throw new DomainException("product_model.image_url_invalid", "Eğitim kiti görsel adresi çok uzun.");
        return new ProductModel(id, name.Trim(), sku.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim(), normalizedImageUrl);
    }

    public void Update(string name, string sku, string? description, string? imageUrl)
    {
        var updated = Create(Id, name, sku, description, imageUrl);
        Name = updated.Name;
        Sku = updated.Sku;
        Description = updated.Description;
        ImageUrl = updated.ImageUrl;
    }
}
