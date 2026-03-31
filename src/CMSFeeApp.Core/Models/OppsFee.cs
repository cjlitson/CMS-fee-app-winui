namespace CMSFeeApp.Core.Models;

public class OppsFee
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string HcpcsCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ApcCode { get; set; }
    public decimal PaymentRate { get; set; }
    public string? StatusIndicator { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}
