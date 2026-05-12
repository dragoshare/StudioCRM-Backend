namespace StudioCRM.Domain.Entities;

public class Trainer
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? Bio { get; set; }

    public string? Phone { get; set; }

    public string Status { get; set; } = "Active";

    public int ExperienceYears { get; set; }

    public string? OutlookCategoryName { get; set; }

    public string? OutlookCategoryColor { get; set; }

    public ICollection<TrainerRate> Rates { get; set; } = new List<TrainerRate>();
    public ICollection<TrainerMonthlySettlement> MonthlySettlements { get; set; } = new List<TrainerMonthlySettlement>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedBy { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Client> Clients { get; set; } = new List<Client>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    public ICollection<TrainerLocation> TrainerLocations { get; set; } = new List<TrainerLocation>();
}
