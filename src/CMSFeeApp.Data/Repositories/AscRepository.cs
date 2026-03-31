using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class AscRepository
{
    private readonly DatabaseContext _context;

    public AscRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<AscFee> GetFees(int year, string? hcpcsCode = null, string? descriptionKeyword = null)
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

        cmd.CommandText = $"SELECT id, year, hcpcs_code, description, payment_rate, data_source, imported_at FROM asc_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code LIMIT 5000";

        var results = new List<AscFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AscFee
            {
                Id = reader.GetInt32(0),
                Year = reader.GetInt32(1),
                HcpcsCode = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                PaymentRate = (decimal)reader.GetDouble(4),
                DataSource = reader.GetString(5),
                ImportedAt = DateTime.Parse(reader.GetString(6))
            });
        }
        return results;
    }

    public IReadOnlyList<int> GetAvailableYears()
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT year FROM asc_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public void InsertFees(IEnumerable<AscFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO asc_fees (year, hcpcs_code, description, payment_rate, data_source, imported_at)
            VALUES (@year, @code, @desc, @payment_rate, @source, @imported_at)
            """;

        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pPaymentRate = cmd.Parameters.Add("@payment_rate", SqliteType.Real);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pYear.Value = fee.Year;
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pPaymentRate.Value = (double)fee.PaymentRate;
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

        cmd.CommandText = $"DELETE FROM asc_fees WHERE {string.Join(" AND ", conditions)}";
        cmd.ExecuteNonQuery();
    }
}
