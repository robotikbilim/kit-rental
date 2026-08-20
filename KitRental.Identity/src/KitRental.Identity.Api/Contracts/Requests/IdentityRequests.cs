using KitRental.Identity.Domain;

namespace KitRental.Identity.Api.Contracts.Requests;

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateUserRequest(string Email, string DisplayName, string Password, UserRole Role, Guid? CustomerId);
public sealed record NotificationRecipientResponse(string Email, string DisplayName);
