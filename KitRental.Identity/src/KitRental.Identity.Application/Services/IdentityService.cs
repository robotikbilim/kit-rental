using KitRental.Identity.Domain;
using KitRental.Security;
using KitRental.SharedKernel;

namespace KitRental.Identity.Application;

public sealed record LoginCommand(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
public sealed record CreateUserCommand(
    string Email,
    string DisplayName,
    string Password,
    UserRole Role,
    Guid? CustomerId);
public sealed record UserResponse(Guid Id, string Email, string DisplayName, UserRole Role, Guid? CustomerId, bool IsActive);

public sealed class IdentityService(
    IUserRepository repository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TokenOptions tokenOptions,
    TimeProvider timeProvider)
{
    public async Task<LoginResponse> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await repository.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !user.IsActive || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new DomainException("authentication.invalid_credentials", "E-posta veya parola hatalı.");
        }

        var now = timeProvider.GetUtcNow();
        var token = tokenService.Create(
            new TokenUser(user.Id, user.Email, user.Role.ToString(), user.CustomerId),
            now);
        return new LoginResponse(token, now.Add(tokenOptions.Lifetime), Map(user));
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.Password.Length < 10)
        {
            throw new DomainException("user.weak_password", "Parola en az 10 karakter olmalıdır.");
        }

        if (await repository.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken) is not null)
        {
            throw new DomainException("user.email_exists", "Bu e-posta adresi zaten kullanılıyor.");
        }

        var user = UserAccount.Create(
            Guid.NewGuid(),
            command.Email,
            command.DisplayName,
            passwordHasher.Hash(command.Password),
            command.Role,
            command.CustomerId);
        await repository.AddAsync(user, cancellationToken);
        return Map(user);
    }

    public async Task<IReadOnlyCollection<UserResponse>> GetUsersAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(Map).ToArray();

    private static UserResponse Map(UserAccount user) =>
        new(user.Id, user.Email, user.DisplayName, user.Role, user.CustomerId, user.IsActive);
}
