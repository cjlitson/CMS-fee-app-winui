using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class ClfsRepository
{
    private readonly DatabaseContext _context;

    public ClfsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<ClfsFee> GetFees(int year, string? hcpcsCode = null, string? descriptionKeyword = null)
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

        cmd.CommandText = $"SELECT id, year, hcpcs_code, description, payment_limit, modifier, data_source, imported_at FROM clfs_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code LIMIT 5000";

        var results = new List<ClfsFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ClfsFee
            {
                Id = reader.GetInt32(0),
                Year = reader.GetInt32(1),
                HcpcsCode = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                PaymentLimit = (decimal)reader.GetDouble(4),
                Modifier = reader.IsDBNull(5) ? null : reader.GetString(5),
                DataSource = reader.GetString(6),
                ImportedAt = DateTime.Parse(reader.GetString(7))
            });
        }
        return results;
    }

    public IReadOnlyList<int> GetAvailableYears()
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT year FROM clfs_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public void InsertFees(IEnumerable<ClfsFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO clfs_fees (year, hcpcs_code, description, payment_limit, modifier, data_source, imported_at)
            VALUES (@year, @code, @desc, @payment_limit, @modifier, @source, @imported_at)
            """;

        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pPaymentLimit = cmd.Parameters.Add("@payment_limit", SqliteType.Real);
        var pModifier = cmd.Parameters.Add("@modifier", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pYear.Value = fee.Year;
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pPaymentLimit.Value = (double)fee.PaymentLimit;
            pModifier.Value = (object?)fee.Modifier ?? DBNull.Value;
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

        cmd.CommandText = $"DELETE FROM clfs_fees WHERE {string.Join(" AND ", conditions)}";
        cmd.ExecuteNonQuery();
    }
}
