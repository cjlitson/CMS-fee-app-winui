namespace CMSFeeApp.Core.Models;

public class FeeColumnDefinition
{
    public string Header { get; init; } = string.Empty;
    public string PropertyName { get; init; } = string.Empty;
    public string? StringFormat { get; init; }
    public int Width { get; init; } = 100;
}
