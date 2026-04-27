namespace StudioCRM.Domain.Entities;

public class TrainerMonthlySettlement
{
    public int Id { get; set; }

    public int TrainerId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public decimal TotalAmount { get; set; }
    public decimal TotalHours { get; set; }
    public int TotalSessions { get; set; }

    public bool IsPaid { get; set; } = false;
    public DateTime? PaidAt { get; set; }
    public int? PaidByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Trainer Trainer { get; set; } = null!;
}