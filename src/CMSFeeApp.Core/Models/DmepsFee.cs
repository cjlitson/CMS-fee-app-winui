namespace CMSFeeApp.Core.Models;

public class DmepsFee
{
    public int Id { get; set; }
    public string HcpcsCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StateAbbr { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal Allowable { get; set; }
    public string? Modifier { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
}
