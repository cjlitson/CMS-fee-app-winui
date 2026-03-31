using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class OppsRepository
{
    private readonly DatabaseContext _context;

    public OppsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<OppsFee> GetFees(int year, string? hcpcsCode = null, string? descriptionKeyword = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string> { "year = @year" };
        cmd.Parameters.AddWithValue("@year", year);

        if (!string.IsNullOrWhiteSpace(hcpcsCode))
        {
            conditions.Add("hcpcs_code LIKE @code");
            cmd.Parameters.AddWithValue("@code", $"%{hcpcsCode.ToUpperInvariant()}%");
        }

        if (!string.IsNullOrWhiteSpace(descriptionKeyword))
        {
            conditions.Add("description LIKE @desc");
            cmd.Parameters.AddWithValue("@desc", $"%{descriptionKeyword}%");
        }

        cmd.CommandText = $"SELECT id, year, hcpcs_code, description, apc_code, payment_rate, status_indicator, data_source, imported_at FROM opps_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code LIMIT 5000";

        var results = new List<OppsFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new OppsFee
            {
                Id = reader.GetInt32(0),
                Year = reader.GetInt32(1),
                HcpcsCode = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                ApcCode = reader.IsDBNull(4) ? null : reader.GetString(4),
                PaymentRate = (decimal)reader.GetDouble(5),
                StatusIndicator = reader.IsDBNull(6) ? null : reader.GetString(6),
                DataSource = reader.GetString(7),
                ImportedAt = DateTime.Parse(reader.GetString(8))
            });
        }
        return results;
    }

    public IReadOnlyList<int> GetAvailableYears()
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT year FROM opps_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public void InsertFees(IEnumerable<OppsFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO opps_fees (year, hcpcs_code, description, apc_code, payment_rate, status_indicator, data_source, imported_at)
            VALUES (@year, @code, @desc, @apc_code, @payment_rate, @status_indicator, @source, @imported_at)
            """;

        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pApcCode = cmd.Parameters.Add("@apc_code", SqliteType.Text);
        var pPaymentRate = cmd.Parameters.Add("@payment_rate", SqliteType.Real);
        var pStatusIndicator = cmd.Parameters.Add("@status_indicator", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pYear.Value = fee.Year;
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pApcCode.Value = (object?)fee.ApcCode ?? DBNull.Value;
            pPaymentRate.Value = (double)fee.PaymentRate;
            pStatusIndicator.Value = (object?)fee.StatusIndicator ?? DBNull.Value;
            pSource.Value = fee.DataSource;
            pImportedAt.Value = fee.ImportedAt.ToString("O");
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteFeesByYear(int year, string? dataSource = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string> { "year = @year" };
        cmd.Parameters.AddWithValue("@year", year);

        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            conditions.Add("data_source = @source");
            cmd.Parameters.AddWithValue("@source", dataSource);
        }

        cmd.CommandText = $"DELETE FROM opps_fees WHERE {string.Join(" AND ", conditions)}";
        cmd.ExecuteNonQuery();
    }
}
