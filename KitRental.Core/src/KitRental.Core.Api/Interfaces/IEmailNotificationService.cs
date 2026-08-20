using KitRental.Core.Domain.Orders;
using KitRental.Core.Domain.Support;

namespace KitRental.Core.Api;

public interface IEmailNotificationService
{
    Task NotifyAdminsOfFaultAsync(FaultTicket ticket, string eventDescription, CancellationToken cancellationToken);
    Task NotifyAdminsOfRentalRequestAsync(RentalOrder order, CancellationToken cancellationToken);
}
