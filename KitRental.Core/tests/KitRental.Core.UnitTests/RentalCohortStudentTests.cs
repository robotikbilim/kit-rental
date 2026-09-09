using KitRental.Core.Domain.Rentals;
using KitRental.SharedKernel;

namespace KitRental.Core.UnitTests;

public sealed class RentalCohortStudentTests
{
    [Fact]
    public void AddStudentAllowsEmptyAddressForPublicAddressCollection()
    {
        var cohort = CreateCohort();

        var student = cohort.AddStudent("Ayşe Yılmaz", "05320000000", string.Empty, Guid.NewGuid());

        Assert.False(student.HasAddress);
        Assert.False(string.IsNullOrWhiteSpace(student.PublicAddressToken));
    }

    [Fact]
    public void UpdateStudentAddressByTokenStoresAddressAndCoordinates()
    {
        var cohort = CreateCohort();
        var student = cohort.AddStudent("Ayşe Yılmaz", "05320000000", string.Empty, Guid.NewGuid());
        var submittedAt = DateTimeOffset.Parse("2026-09-07T12:00:00+03:00");

        cohort.UpdateStudentAddressByToken(student.PublicAddressToken, "Test Sokak 1", 41.012345, 29.012345,
            submittedAt);

        Assert.True(student.HasAddress);
        Assert.Equal("Test Sokak 1", student.AddressLine);
        Assert.Equal(41.012345, student.Latitude);
        Assert.Equal(29.012345, student.Longitude);
        Assert.Equal(submittedAt, student.AddressSubmittedAt);
    }

    [Fact]
    public void UpdateStudentAddressByTokenAllowsAddressWithoutValidCoordinates()
    {
        var cohort = CreateCohort();
        var student = cohort.AddStudent("Ayşe Yılmaz", "05320000000", string.Empty, Guid.NewGuid());
        var submittedAt = DateTimeOffset.Parse("2026-09-07T12:00:00+03:00");

        cohort.UpdateStudentAddressByToken(student.PublicAddressToken, "Test Sokak 1", 99, null, submittedAt);

        Assert.True(student.HasAddress);
        Assert.Equal("Test Sokak 1", student.AddressLine);
        Assert.Null(student.Latitude);
        Assert.Null(student.Longitude);
        Assert.Equal(submittedAt, student.AddressSubmittedAt);
    }

    [Fact]
    public void UpdateStudentAddressByTokenRejectsUnknownToken()
    {
        var cohort = CreateCohort();

        var exception = Assert.Throws<DomainException>(() =>
            cohort.UpdateStudentAddressByToken("missing", "Test Sokak 1", null, null, DateTimeOffset.UtcNow));

        Assert.Equal("rental_cohort.student_not_found", exception.Code);
    }

    private static RentalCohort CreateCohort() =>
        RentalCohort.Create(Guid.NewGuid(), Guid.NewGuid(), "2026 Güz", new DateOnly(2026, 9, 1),
            new DateOnly(2026, 12, 31), DateTimeOffset.Parse("2026-09-07T12:00:00+03:00"));
}
