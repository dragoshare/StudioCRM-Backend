namespace StudioCRM.Application.DTOs.SessionParticipants;
public class PrepareGroupEnrollmentRequest
{
    public int ClientId { get; set; }
    public int PackageId { get; set; }
    public int? ClientPackageId { get; set; }
}
public record GroupEnrollmentDto(int Id, int SessionId, int ClientId, int ClientPackageId,
    string Status, decimal AmountDue, bool ReservesSeat);
