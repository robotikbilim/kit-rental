using KitRental.Identity.Application;
using KitRental.Identity.Domain;
using KitRental.Security;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace KitRental.Identity.Infrastructure;

public interface IIdentityStoreInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class MongoUserRepository : IUserRepository, IIdentityStoreInitializer
{
    private readonly IMongoCollection<UserDocument> _users;
    private readonly IPasswordHasher _passwordHasher;

    public MongoUserRepository(IMongoDatabase database, IPasswordHasher passwordHasher)
    {
        _users = database.GetCollection<UserDocument>("users");
        _passwordHasher = passwordHasher;
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var document = await _users.Find(user => user.Email == email).FirstOrDefaultAsync(cancellationToken);
        return document?.ToDomain();
    }

    public async Task<IReadOnlyCollection<UserAccount>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _users.Find(FilterDefinition<UserDocument>.Empty).SortBy(user => user.Email).ToListAsync(cancellationToken))
        .Select(user => user.ToDomain())
        .ToArray();

    public async Task AddAsync(UserAccount user, CancellationToken cancellationToken)
    {
        try
        {
            await _users.InsertOneAsync(UserDocument.FromDomain(user), cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException("E-posta adresi benzersiz olmalıdır.", exception);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var emailIndex = new CreateIndexModel<UserDocument>(
            Builders<UserDocument>.IndexKeys.Ascending(user => user.Email),
            new CreateIndexOptions { Unique = true, Name = "ux_users_email" });
        await _users.Indexes.CreateOneAsync(emailIndex, cancellationToken: cancellationToken);

        await EnsureDevelopmentUserAsync(UserAccount.Create(
            InMemoryUserRepository.DevelopmentAdminId, "admin@robotikbilim.com.tr", "Sistem Yöneticisi",
            _passwordHasher.Hash("41yaD3r!n58"), UserRole.SystemAdmin, null), cancellationToken);
        await EnsureDevelopmentUserAsync(UserAccount.Create(
            InMemoryUserRepository.DevelopmentTacevUserId, "kadikoy@tacev.demo", "TACEV Kadıköy",
            _passwordHasher.Hash("Tacev12345!"), UserRole.CustomerAccountManager,
            InMemoryUserRepository.DevelopmentTacevCustomerId), cancellationToken);
    }

    private async Task EnsureDevelopmentUserAsync(UserAccount account, CancellationToken cancellationToken)
    {
        if (await _users.Find(user => user.Email == account.Email).AnyAsync(cancellationToken))
            return;

        var existingSeedUser = await _users.Find(user => user.Id == account.Id).AnyAsync(cancellationToken);
        if (existingSeedUser)
        {
            await _users.ReplaceOneAsync(
                user => user.Id == account.Id,
                UserDocument.FromDomain(account),
                cancellationToken: cancellationToken);
            return;
        }

        await _users.InsertOneAsync(UserDocument.FromDomain(account), cancellationToken: cancellationToken);
    }

    private sealed class UserDocument
    {
        [BsonId]
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string PasswordHash { get; init; } = string.Empty;
        public UserRole Role { get; init; }
        [BsonGuidRepresentation(GuidRepresentation.Standard)]
        public Guid? CustomerId { get; init; }
        public bool IsActive { get; init; }

        public static UserDocument FromDomain(UserAccount user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            Role = user.Role,
            CustomerId = user.CustomerId,
            IsActive = user.IsActive
        };

        public UserAccount ToDomain()
        {
            var user = UserAccount.Create(Id, Email, DisplayName, PasswordHash, Role, CustomerId);
            if (!IsActive)
                user.Deactivate();
            return user;
        }
    }
}
