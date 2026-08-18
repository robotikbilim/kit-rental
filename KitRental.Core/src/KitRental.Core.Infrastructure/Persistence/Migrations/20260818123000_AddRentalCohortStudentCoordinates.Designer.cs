using KitRental.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KitRental.Core.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(KitRentalDbContext))]
    [Migration("20260818123000_AddRentalCohortStudentCoordinates")]
    public partial class AddRentalCohortStudentCoordinates
    {
    }
}
