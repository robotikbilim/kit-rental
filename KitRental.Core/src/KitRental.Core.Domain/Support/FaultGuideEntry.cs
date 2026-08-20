using KitRental.SharedKernel;

namespace KitRental.Core.Domain.Support;

public sealed class FaultGuideEntry
{
    private FaultGuideEntry() { }

    private FaultGuideEntry(Guid id, Guid? productModelId, string title, string problem, string solution, int displayOrder)
    {
        Id = id;
        ProductModelId = productModelId;
        Title = Clean(title, 160, "fault_guide.title_required", "Baslik zorunludur.");
        Problem = Clean(problem, 2000, "fault_guide.problem_required", "Karsilasilan problem zorunludur.");
        Solution = Clean(solution, 4000, "fault_guide.solution_required", "Cozum onerisi zorunludur.");
        DisplayOrder = displayOrder;
        IsActive = true;
        CreatedAt = TurkeyTime.Now();
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid? ProductModelId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Problem { get; private set; } = string.Empty;
    public string Solution { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static FaultGuideEntry Create(Guid id, string title, string problem, string solution, int displayOrder,
        Guid? productModelId = null)
    {
        if (id == Guid.Empty)
            throw new DomainException("fault_guide.id_required", "Gecerli bir rehber kaydi zorunludur.");
        return new FaultGuideEntry(id, productModelId, title, problem, solution, displayOrder);
    }

    public void Update(string title, string problem, string solution, int displayOrder, bool isActive,
        Guid? productModelId = null)
    {
        ProductModelId = productModelId;
        Title = Clean(title, 160, "fault_guide.title_required", "Baslik zorunludur.");
        Problem = Clean(problem, 2000, "fault_guide.problem_required", "Karsilasilan problem zorunludur.");
        Solution = Clean(solution, 4000, "fault_guide.solution_required", "Cozum onerisi zorunludur.");
        DisplayOrder = displayOrder;
        IsActive = isActive;
        UpdatedAt = TurkeyTime.Now();
    }

    private static string Clean(string value, int maxLength, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException(code, message);
        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
            throw new DomainException("fault_guide.too_long", $"Metin en fazla {maxLength} karakter olabilir.");
        return cleaned;
    }
}
