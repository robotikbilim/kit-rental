using KitRental.Core.Application.Common;

namespace KitRental.Core.Api.Controllers;

internal static class PagingExtensions
{
    public static PagedResponse<T> ToPagedResponse<T>(
        this IEnumerable<T> source,
        int? page,
        int? pageSize)
    {
        var items = source as T[] ?? source.ToArray();
        var validPageSize = Math.Clamp(pageSize ?? 20, 1, 5000);
        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Length / (double)validPageSize));
        var validPage = Math.Clamp(page ?? 1, 1, totalPages);

        return new PagedResponse<T>(
            validPage,
            validPageSize,
            items.Length,
            totalPages,
            items.Skip((validPage - 1) * validPageSize).Take(validPageSize).ToArray());
    }
}
