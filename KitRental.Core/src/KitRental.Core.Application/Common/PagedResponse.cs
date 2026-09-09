namespace KitRental.Core.Application.Common;

public sealed record PagedResponse<T>(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<T> Items);
