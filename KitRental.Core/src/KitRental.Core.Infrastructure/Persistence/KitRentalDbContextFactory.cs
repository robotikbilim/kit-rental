using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KitRental.Core.Infrastructure.Persistence;

public sealed class KitRentalDbContextFactory : IDesignTimeDbContextFactory<KitRentalDbContext>
{
    public KitRentalDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CoreDatabase")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=KitRentalCore;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<KitRentalDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new KitRentalDbContext(options);
    }
}
