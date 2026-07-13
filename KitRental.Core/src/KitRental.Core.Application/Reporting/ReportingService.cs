using System.Text;
using KitRental.Core.Application.Abstractions;
using KitRental.Core.Domain.Auditing;

namespace KitRental.Core.Application.Reporting;

public sealed class ReportingService(ICoreRepository repository)
{
    public Task<IReadOnlyCollection<AuditEntry>> GetAuditTrailAsync(CancellationToken cancellationToken) =>
        repository.GetAuditEntriesAsync(cancellationToken);

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
