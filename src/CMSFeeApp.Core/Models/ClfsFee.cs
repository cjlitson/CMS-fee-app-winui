namespace CMSFeeApp.Core.Models;

public class ClfsFee
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string HcpcsCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PaymentLimit { get; set; }
    public string? Modifier { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}
