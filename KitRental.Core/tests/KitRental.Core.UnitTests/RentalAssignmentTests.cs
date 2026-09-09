using KitRental.Core.Domain.Rentals;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class RentalAssignmentTests
{
    [Fact]
    public void ActivateReservedAssignmentMarksItActive()
    {
        var assignment = CreateAssignment();

        assignment.Activate();

        Assert.Equal(RentalAssignmentStatus.Active, assignment.Status);
        Assert.True(assignment.BlocksAvailability);
    }

    [Fact]
    public void CompleteActiveAssignmentReleasesAvailabilityBlock()
    {
        var assignment = CreateAssignment();
        assignment.Activate();

        assignment.Complete();

        Assert.Equal(RentalAssignmentStatus.Completed, assignment.Status);
        Assert.False(assignment.BlocksAvailability);
    }

    [Fact]
    public void CompleteReservedAssignmentIsRejected()
    {
        var assignment = CreateAssignment();

        Assert.Throws<DomainException>(assignment.Complete);
    }

    private static RentalAssignment CreateAssignment() => RentalAssignment.Create(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), new RentalPeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)),
        DateTimeOffset.UtcNow, Guid.NewGuid());
}
