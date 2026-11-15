namespace PRN232.Domain.Entities;

public class IdentityDocuments
{
    public int IdentityDocumentId
    {
        get; set;
    }
    public int CustomerId
    {
        get; set;
    }
    // Lưu dạng mã hóa (Data Protection, qua value converter ở AppDbContext) — A9.
    public string CCCDNumber { get; set; } = string.Empty;
    // HMAC-SHA256 của số CCCD để kiểm tra trùng mà không cần giải mã (blind index).
    public string? CCCDNumberHash { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? ReviewedBy
    {
        get; set;
    }
    // V3: reason given when the document is rejected; shown to the customer. Null otherwise.
    public string? ReviewNote { get; set; }

    public Users? Customer
    {
        get; set;
    }
    public Users? ReviewedByUser
    {
        get; set;
    }
}
