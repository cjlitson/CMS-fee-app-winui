namespace CMSFeeApp.Core.Models;

public class AscFee
{
    public int Id { get; set; }
    public int Year { get; set; }
    public string HcpcsCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PaymentRate { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}
