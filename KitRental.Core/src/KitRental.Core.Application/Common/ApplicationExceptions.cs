namespace KitRental.Core.Application.Common;

public sealed class ResourceNotFoundException(string message) : Exception(message);

public sealed class ConflictException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ForbiddenException(string message) : Exception(message);
