using ClosedXML.Excel;
using CMSFeeApp.Core;
using CMSFeeApp.Core.Interfaces;
using CMSFeeApp.Core.Models;
using CMSFeeApp.Data.Repositories;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CMSFeeApp.Data.Services;

/// <summary>
/// Exports DMEPOS or PFS fee schedule data to CSV, XLSX, or PDF.
/// Implements IFeeExporter for both schedule types.
/// </summary>
public class FeeExportService : IFeeExporter
{
    private readonly FeeScheduleType _scheduleType;
    private readonly DmeposRepository? _dmeposRepository;
    private readonly PfsRepository? _pfsRepository;

    private static readonly IReadOnlyList<ExportFormat> _allFormats = [ExportFormat.Csv, ExportFormat.Xlsx, ExportFormat.Pdf];

    static FeeExportService()
    {
        // QuestPDF community license — required before use
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public FeeScheduleType ScheduleType => _scheduleType;
    public IReadOnlyList<ExportFormat> SupportedFormats => _allFormats;

    public FeeExportService(FeeScheduleType scheduleType, DmeposRepository dmeposRepository)
    {
        _scheduleType = scheduleType;
        _dmeposRepository = dmeposRepository;
    }

    public FeeExportService(FeeScheduleType scheduleType, PfsRepository pfsRepository)
    {
        _scheduleType = scheduleType;
        _pfsRepository = pfsRepository;
    }

    public async Task ExportAsync(ExportFormat format, string outputPath, int year, string? stateAbbr, CancellationToken ct = default)
    {
        await Task.Run(() =>
        {
            if (_scheduleType == FeeScheduleType.Dmepos)
            {
                var fees = _dmeposRepository!.GetFees(year, stateAbbr);
                ExportDmepos(format, outputPath, fees);
            }
            else
            {
                var fees = _pfsRepository!.GetFees(year);
                ExportPfs(format, outputPath, fees);
            }
        }, ct);
    }

    // -------------------------------------------------------------------------
    // DMEPOS export
    // -------------------------------------------------------------------------

    private static void ExportDmepos(ExportFormat format, string outputPath, IReadOnlyList<DmepsFee> fees)
    {
        switch (format)
        {
            case ExportFormat.Csv: ExportDmeposCsv(outputPath, fees); break;
            case ExportFormat.Xlsx: ExportDmeposXlsx(outputPath, fees); break;
            case ExportFormat.Pdf: ExportDmeposPdf(outputPath, fees); break;
        }
    }

    private static void ExportDmeposCsv(string outputPath, IReadOnlyList<DmepsFee> fees)
    {
        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("HCPCS Code,Description,State,Year,Allowable,Allowable NR,Allowable R,Modifier");
        foreach (var f in fees)
        {
            writer.WriteLine(string.Join(",",
                CsvEscape(f.HcpcsCode),
                CsvEscape(f.Description),
                CsvEscape(f.StateAbbr),
                f.Year,
                f.Allowable,
                f.AllowableNr.HasValue ? f.AllowableNr.Value.ToString("F2") : "",
                f.AllowableR.HasValue ? f.AllowableR.Value.ToString("F2") : "",
                CsvEscape(f.Modifier)));
        }
    }

    private static void ExportDmeposXlsx(string outputPath, IReadOnlyList<DmepsFee> fees)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("DMEPOS Fee Schedule");

        var headers = new[] { "HCPCS Code", "Description", "State", "Year", "Allowable", "Allowable NR", "Allowable R", "Modifier" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#003366");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (int row = 0; row < fees.Count; row++)
        {
            var f = fees[row];
            int r = row + 2;
            ws.Cell(r, 1).Value = f.HcpcsCode;
            ws.Cell(r, 2).Value = f.Description ?? "";
            ws.Cell(r, 3).Value = f.StateAbbr;
            ws.Cell(r, 4).Value = f.Year;
            ws.Cell(r, 5).Value = (double)f.Allowable;
            if (f.AllowableNr.HasValue) ws.Cell(r, 6).Value = (double)f.AllowableNr.Value;
            if (f.AllowableR.HasValue) ws.Cell(r, 7).Value = (double)f.AllowableR.Value;
            ws.Cell(r, 8).Value = f.Modifier ?? "";

            if (row % 2 == 1)
                ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        }

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = Math.Min(ws.Column(2).Width, 60);
        wb.SaveAs(outputPath);
    }

    private static void ExportDmeposPdf(string outputPath, IReadOnlyList<DmepsFee> fees)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0.5f, Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text("CMS DMEPOS Fee Schedule Report")
                        .SemiBold().FontSize(14).FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"Generated: {DateTime.Now:MMMM dd, yyyy}  |  {fees.Count:N0} records")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);
                            cols.RelativeColumn();
                            cols.ConstantColumn(40);
                            cols.ConstantColumn(40);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(70);
                            cols.ConstantColumn(50);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "HCPCS", "Description", "State", "Year", "Allowable", "NR", "R", "Modifier" })
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .Text(h).Bold().FontColor(Colors.White).FontSize(8);
                        });
                        bool alt = false;
                        foreach (var f in fees)
                        {
                            alt = !alt;
                            var bg = alt ? Colors.White : Colors.Grey.Lighten4;
                            table.Cell().Background(bg).Padding(3).Text(f.HcpcsCode).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(TruncateDescription(f.Description)).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.StateAbbr).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.Year.ToString()).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.Allowable.ToString("C")).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.AllowableNr.HasValue ? f.AllowableNr.Value.ToString("C") : "").FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.AllowableR.HasValue ? f.AllowableR.Value.ToString("C") : "").FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.Modifier ?? "").FontSize(7);
                        }
                    });
                });
            });
        }).GeneratePdf(outputPath);
    }

    // -------------------------------------------------------------------------
    // PFS export
    // -------------------------------------------------------------------------

    private static void ExportPfs(ExportFormat format, string outputPath, IReadOnlyList<PfsFee> fees)
    {
        switch (format)
        {
            case ExportFormat.Csv: ExportPfsCsv(outputPath, fees); break;
            case ExportFormat.Xlsx: ExportPfsXlsx(outputPath, fees); break;
            case ExportFormat.Pdf: ExportPfsPdf(outputPath, fees); break;
        }
    }

    private static void ExportPfsCsv(string outputPath, IReadOnlyList<PfsFee> fees)
    {
        using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("HCPCS Code,Description,Year,Non-Facility Payment,Facility Payment,Modifier");
        foreach (var f in fees)
        {
            writer.WriteLine(string.Join(",",
                CsvEscape(f.HcpcsCode),
                CsvEscape(f.Description),
                f.Year,
                f.PaymentNonFacility,
                f.PaymentFacility,
                CsvEscape(f.Modifier)));
        }
    }

    private static void ExportPfsXlsx(string outputPath, IReadOnlyList<PfsFee> fees)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("PFS Fee Schedule");

        var headers = new[] { "HCPCS Code", "Description", "Year", "Non-Facility Payment", "Facility Payment", "Modifier" };
        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#003366");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (int row = 0; row < fees.Count; row++)
        {
            var f = fees[row];
            int r = row + 2;
            ws.Cell(r, 1).Value = f.HcpcsCode;
            ws.Cell(r, 2).Value = f.Description ?? "";
            ws.Cell(r, 3).Value = f.Year;
            ws.Cell(r, 4).Value = (double)f.PaymentNonFacility;
            ws.Cell(r, 5).Value = (double)f.PaymentFacility;
            ws.Cell(r, 6).Value = f.Modifier ?? "";

            if (row % 2 == 1)
                ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        }

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = Math.Min(ws.Column(2).Width, 60);
        wb.SaveAs(outputPath);
    }

    private static void ExportPfsPdf(string outputPath, IReadOnlyList<PfsFee> fees)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0.5f, Unit.Centimetre);
                page.Content().Column(col =>
                {
                    col.Item().Text("CMS Physician Fee Schedule (PFS) Report")
                        .SemiBold().FontSize(14).FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"Generated: {DateTime.Now:MMMM dd, yyyy}  |  {fees.Count:N0} records")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(70);
                            cols.RelativeColumn();
                            cols.ConstantColumn(40);
                            cols.ConstantColumn(90);
                            cols.ConstantColumn(90);
                            cols.ConstantColumn(50);
                        });
                        table.Header(header =>
                        {
                            foreach (var h in new[] { "HCPCS", "Description", "Year", "Non-Facility", "Facility", "Modifier" })
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .Text(h).Bold().FontColor(Colors.White).FontSize(8);
                        });
                        bool alt = false;
                        foreach (var f in fees)
                        {
                            alt = !alt;
                            var bg = alt ? Colors.White : Colors.Grey.Lighten4;
                            table.Cell().Background(bg).Padding(3).Text(f.HcpcsCode).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(TruncateDescription(f.Description)).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.Year.ToString()).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.PaymentNonFacility.ToString("C")).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.PaymentFacility.ToString("C")).FontSize(7);
                            table.Cell().Background(bg).Padding(3).Text(f.Modifier ?? "").FontSize(7);
                        }
                    });
                });
            });
        }).GeneratePdf(outputPath);
    }

    private static string TruncateDescription(string? desc) =>
        desc is null ? "" : desc.Length > 60 ? desc[..60] + "…" : desc;

    private static string CsvEscape(string? value)
    {
        if (value is null) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
