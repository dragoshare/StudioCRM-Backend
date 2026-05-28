namespace StudioCRM.Application.DTOs.TrainerSettlements;

public class TrainerWorkHoursDocumentDto
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public byte[] Content { get; set; } = [];
}
