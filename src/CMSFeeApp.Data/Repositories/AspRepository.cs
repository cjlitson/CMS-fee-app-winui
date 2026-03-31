using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class AspRepository
{
    private readonly DatabaseContext _context;

    public AspRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<AspFee> GetFees(int year, int? quarter = null, string? hcpcsCode = null, string? descriptionKeyword = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string> { "year = @year" };
        cmd.Parameters.AddWithValue("@year", year);

        if (quarter.HasValue)
        {
            conditions.Add("quarter = @quarter");
            cmd.Parameters.AddWithValue("@quarter", quarter.Value);
        }

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

        cmd.CommandText = $"SELECT id, year, quarter, hcpcs_code, description, payment_limit, dosage_descriptor, data_source, imported_at FROM asp_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code, quarter LIMIT 5000";

        var results = new List<AspFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AspFee
            {
                Id = reader.GetInt32(0),
                Year = reader.GetInt32(1),
                Quarter = reader.GetInt32(2),
                HcpcsCode = reader.GetString(3),
                Description = reader.IsDBNull(4) ? null : reader.GetString(4),
                PaymentLimit = (decimal)reader.GetDouble(5),
                DosageDescriptor = reader.IsDBNull(6) ? null : reader.GetString(6),
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
        cmd.CommandText = "SELECT DISTINCT year FROM asp_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public void InsertFees(IEnumerable<AspFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO asp_fees (year, quarter, hcpcs_code, description, payment_limit, dosage_descriptor, data_source, imported_at)
            VALUES (@year, @quarter, @code, @desc, @payment_limit, @dosage, @source, @imported_at)
            """;

        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pQuarter = cmd.Parameters.Add("@quarter", SqliteType.Integer);
        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pPaymentLimit = cmd.Parameters.Add("@payment_limit", SqliteType.Real);
        var pDosage = cmd.Parameters.Add("@dosage", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pYear.Value = fee.Year;
            pQuarter.Value = fee.Quarter;
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pPaymentLimit.Value = (double)fee.PaymentLimit;
            pDosage.Value = (object?)fee.DosageDescriptor ?? DBNull.Value;
            pSource.Value = fee.DataSource;
            pImportedAt.Value = fee.ImportedAt.ToString("O");
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void DeleteFeesByYear(int year, int? quarter = null, string? dataSource = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string> { "year = @year" };
        cmd.Parameters.AddWithValue("@year", year);

        if (quarter.HasValue)
        {
            conditions.Add("quarter = @quarter");
            cmd.Parameters.AddWithValue("@quarter", quarter.Value);
        }

        if (!string.IsNullOrWhiteSpace(dataSource))
        {
            conditions.Add("data_source = @source");
            cmd.Parameters.AddWithValue("@source", dataSource);
        }

        cmd.CommandText = $"DELETE FROM asp_fees WHERE {string.Join(" AND ", conditions)}";
        cmd.ExecuteNonQuery();
    }
}
