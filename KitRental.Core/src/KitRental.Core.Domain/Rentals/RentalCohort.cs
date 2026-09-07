using KitRental.SharedKernel;
using System.Security.Cryptography;

namespace KitRental.Core.Domain.Rentals;

public sealed class RentalCohort
{
    private readonly List<RentalCohortStudent> _students = [];

    private RentalCohort()
    {
    }

    private RentalCohort(Guid id, Guid customerId, string name, DateOnly startDate, DateOnly endDate,
        DateTimeOffset createdAt)
    {
        Id = id;
        CustomerId = customerId;
        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<RentalCohortStudent> Students => _students.AsReadOnly();

    public static RentalCohort Create(Guid id, Guid customerId, string name, DateOnly startDate,
        DateOnly endDate, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty || customerId == Guid.Empty || string.IsNullOrWhiteSpace(name))
            throw new DomainException("rental_cohort.required_fields", "Dönem adı ve müşteri zorunludur.");
        if (endDate <= startDate)
            throw new DomainException("rental_cohort.invalid_period", "Dönem bitiş tarihi başlangıçtan sonra olmalıdır.");

        return new RentalCohort(id, customerId, name, startDate, endDate, createdAt);
    }

    public void Update(string name, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("rental_cohort.required_fields", "Dönem adı zorunludur.");
        if (endDate <= startDate)
            throw new DomainException("rental_cohort.invalid_period", "Dönem bitiş tarihi başlangıçtan sonra olmalıdır.");

        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    public RentalCohortStudent AddStudent(string fullName, string guardianPhone, string addressLine,
        Guid productModelId)
    {
        var student = RentalCohortStudent.Create(Guid.NewGuid(), Id, fullName, guardianPhone, addressLine,
            productModelId);
        _students.Add(student);
        return student;
    }

    public RentalCohortStudent UpdateStudent(Guid studentId, string fullName, string guardianPhone,
        string addressLine, Guid productModelId)
    {
        var student = GetStudent(studentId);
        student.Update(fullName, guardianPhone, addressLine, productModelId);
        return student;
    }

    public void RemoveStudent(Guid studentId)
    {
        var student = GetStudent(studentId);
        if (student.HasKitAssignment)
            student.UnassignAndAnonymize();
        else
            _students.Remove(student);
    }

    public void LinkStudentToKit(Guid studentId, Guid orderId, Guid assignmentId, Guid productUnitId)
    {
        GetStudent(studentId).LinkKit(orderId, assignmentId, productUnitId);
    }

    public RentalCohortStudent? UnlinkStudentKit(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
            throw new DomainException("rental_cohort.assignment_required", "Kiralama ataması zorunludur.");
        var student = _students.SingleOrDefault(item => item.AssignmentId == assignmentId && !item.IsDeleted);
        student?.UnlinkKit();
        return student;
    }

    public void LinkActiveStudentsToOrder(Guid orderId)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("rental_cohort.order_required", "Sipariş kimliği zorunludur.");
        foreach (var student in _students.Where(item => !item.IsDeleted))
            student.LinkOrder(orderId);
    }

    public RentalCohortStudent UpdateStudentAddressByToken(string publicAddressToken, string addressLine,
        double? latitude, double? longitude, DateTimeOffset submittedAt)
    {
        var student = _students.SingleOrDefault(item =>
            !item.IsDeleted &&
            string.Equals(item.PublicAddressToken, publicAddressToken.Trim(), StringComparison.Ordinal))
            ?? throw new DomainException("rental_cohort.student_not_found", "Öğrenci bulunamadı.");
        student.UpdatePublicAddress(addressLine, latitude, longitude, submittedAt);
        return student;
    }

    private RentalCohortStudent GetStudent(Guid studentId) =>
        _students.SingleOrDefault(item => item.Id == studentId)
        ?? throw new DomainException("rental_cohort.student_not_found", "Öğrenci bulunamadı.");
}

public sealed class RentalCohortStudent
{
    private RentalCohortStudent()
    {
    }

    private RentalCohortStudent(Guid id, Guid rentalCohortId, string fullName, string guardianPhone,
        string addressLine, Guid productModelId)
    {
        Id = id;
        RentalCohortId = rentalCohortId;
        FullName = fullName.Trim();
        GuardianPhone = TurkishPhoneNumber.Normalize(guardianPhone, "Veli telefon numarası");
        AddressLine = addressLine?.Trim() ?? string.Empty;
        ProductModelId = productModelId;
        PublicAddressToken = CreatePublicAddressToken();
    }

    public Guid Id { get; private set; }
    public Guid RentalCohortId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string GuardianPhone { get; private set; } = string.Empty;
    public string AddressLine { get; private set; } = string.Empty;
    public Guid ProductModelId { get; private set; }
    public Guid? OrderId { get; private set; }
    public Guid? AssignmentId { get; private set; }
    public Guid? ProductUnitId { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string PublicAddressToken { get; private set; } = string.Empty;
    public DateTimeOffset? AddressSubmittedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public bool HasKitAssignment => AssignmentId.HasValue && ProductUnitId.HasValue;
    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
    public bool HasAddress => !string.IsNullOrWhiteSpace(AddressLine);

    public static RentalCohortStudent Create(Guid id, Guid rentalCohortId, string fullName,
        string guardianPhone, string addressLine, Guid productModelId)
    {
        if (id == Guid.Empty || rentalCohortId == Guid.Empty || productModelId == Guid.Empty ||
            string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(guardianPhone))
            throw new DomainException("rental_cohort_student.required_fields",
                "Öğrenci adı, veli telefonu ve eğitim kiti zorunludur.");

        return new RentalCohortStudent(id, rentalCohortId, fullName, guardianPhone, addressLine, productModelId);
    }

    public void Update(string fullName, string guardianPhone, string addressLine, Guid productModelId)
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenci güncellenemez.");
        if (HasKitAssignment && productModelId != ProductModelId)
            throw new DomainException("rental_cohort_student.kit_locked",
                "Kit ataması yapıldıktan sonra eğitim kiti değiştirilemez.");

        var updated = Create(Id, RentalCohortId, fullName, guardianPhone, addressLine, productModelId);
        FullName = updated.FullName;
        GuardianPhone = updated.GuardianPhone;
        AddressLine = updated.AddressLine;
        ProductModelId = updated.ProductModelId;
    }

    public void UpdatePublicAddress(string addressLine, double? latitude, double? longitude,
        DateTimeOffset submittedAt)
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenci güncellenemez.");
        if (string.IsNullOrWhiteSpace(addressLine))
            throw new DomainException("rental_cohort_student.address_required", "Adres zorunludur.");
        AddressLine = addressLine.Trim();
        if (latitude.HasValue && longitude.HasValue && CoordinatesAreValid(latitude.Value, longitude.Value))
        {
            Latitude = latitude;
            Longitude = longitude;
        }
        else
        {
            Latitude = null;
            Longitude = null;
        }
        AddressSubmittedAt = submittedAt;
    }

    public void UpdateCoordinates(double latitude, double longitude)
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenci güncellenemez.");
        if (!CoordinatesAreValid(latitude, longitude))
            throw new DomainException("rental_cohort_student.invalid_coordinates", "Öğrenci konum bilgisi geçersiz.");

        Latitude = latitude;
        Longitude = longitude;
    }

    public void LinkKit(Guid orderId, Guid assignmentId, Guid productUnitId)
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenciye kit atanamaz.");
        if (orderId == Guid.Empty || assignmentId == Guid.Empty || productUnitId == Guid.Empty)
            throw new DomainException("rental_cohort_student.assignment_required", "Kit ataması geçersiz.");
        if (HasKitAssignment)
            throw new DomainException("rental_cohort_student.already_assigned", "Öğrenciye zaten kit atanmış.");

        OrderId = orderId;
        AssignmentId = assignmentId;
        ProductUnitId = productUnitId;
    }

    public void LinkOrder(Guid orderId)
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenci siparişe bağlanamaz.");
        if (orderId == Guid.Empty)
            throw new DomainException("rental_cohort_student.order_required", "Sipariş kimliği zorunludur.");
        if (OrderId.HasValue && OrderId.Value != orderId)
            throw new DomainException("rental_cohort_student.order_locked", "Öğrenci zaten başka bir siparişe bağlı.");

        OrderId = orderId;
    }

    public void UnassignAndAnonymize()
    {
        FullName = string.Empty;
        GuardianPhone = string.Empty;
        AddressLine = string.Empty;
        Latitude = null;
        Longitude = null;
        AddressSubmittedAt = null;
        IsDeleted = true;
    }

    public void UnlinkKit()
    {
        if (IsDeleted)
            throw new DomainException("rental_cohort_student.deleted", "Silinmiş öğrenci güncellenemez.");

        AssignmentId = null;
        ProductUnitId = null;
    }

    private static bool CoordinatesAreValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string CreatePublicAddressToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
