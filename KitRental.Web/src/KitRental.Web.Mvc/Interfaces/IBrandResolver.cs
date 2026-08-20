using KitRental.Web.Mvc.Branding;

namespace KitRental.Web.Mvc.Branding;

public interface IBrandResolver
{
    BrandDefinition Current { get; }
}
