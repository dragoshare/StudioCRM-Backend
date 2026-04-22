namespace StudioCRM.Domain.Entities;

public class Location
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TrainerLocation> TrainerLocations { get; set; } = new List<TrainerLocation>();

    public ICollection<Client> Clients { get; set; } = new List<Client>();

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}