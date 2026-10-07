namespace PRN232.Domain.Entities;

public class IdentityDocuments
{
    public int IdentityDocumentId { get; set; }
    public int CustomerId { get; set; }
    public string CCCDNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? ReviewedBy { get; set; }

    public Users? Customer { get; set; }
    public Users? ReviewedByUser { get; set; }
}
