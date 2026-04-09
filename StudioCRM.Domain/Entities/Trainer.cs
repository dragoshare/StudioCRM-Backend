using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudioCRM.Domain.Entities;

public class Trainer
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Specialization { get; set; } = string.Empty;

    public string EmploymentType { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;

    public ICollection<Client> Clients { get; set; } = new List<Client>();
}
