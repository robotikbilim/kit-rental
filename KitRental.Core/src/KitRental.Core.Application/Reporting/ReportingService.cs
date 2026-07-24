using System.Text;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Auditing;

namespace KitRental.Core.Application.Reporting;

public sealed class ReportingService(ICoreRepository repository)
{
    public async Task<AuditPage> GetAuditTrailAsync(AuditQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);
        var result = await repository.GetAuditEntriesAsync(
            string.IsNullOrWhiteSpace(query.Action) ? null : query.Action.Trim(),
            query.ActorId, query.OccurredFrom, query.OccurredTo, page, pageSize, cancellationToken);
        return new AuditPage(page, pageSize, result.TotalCount,
            Math.Max(1, (int)Math.Ceiling(result.TotalCount / (double)pageSize)), result.Items);
    }

    public async Task<byte[]> ExportInventoryCsvAsync(CancellationToken cancellationToken)
    {
        var units = await repository.GetProductUnitsAsync(cancellationToken);
        var csv = new StringBuilder("Id,ProductModelId,SerialNumber,QrCode,Status\r\n");
        foreach (var unit in units.OrderBy(item => item.SerialNumber))
        {
            csv.Append(Escape(unit.Id.ToString())).Append(',')
                .Append(Escape(unit.ProductModelId.ToString())).Append(',')
                .Append(Escape(unit.SerialNumber)).Append(',')
                .Append(Escape(unit.QrCode)).Append(',')
                .Append(Escape(unit.Status.ToString())).Append("\r\n");
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@')
            value = $"'{value}";
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}

public sealed record AuditQuery(string? Action, Guid? ActorId, DateTimeOffset? OccurredFrom,
    DateTimeOffset? OccurredTo, int Page = 1, int PageSize = 25);
public sealed record AuditPage(int Page, int PageSize, int TotalCount, int TotalPages,
    IReadOnlyCollection<AuditEntry> Items);
