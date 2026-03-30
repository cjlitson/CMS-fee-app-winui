using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class PfsRepository
{
    private readonly DatabaseContext _context;

    public PfsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<PfsFee> GetFees(int year, string? hcpcsCode = null, string? descriptionKeyword = null)
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

        cmd.CommandText = $"SELECT id, year, hcpcs_code, description, payment_non_facility, payment_facility, modifier, data_source, imported_at FROM pfs_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code LIMIT 5000";

        var results = new List<PfsFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new PfsFee
            {
                Id = reader.GetInt32(0),
                Year = reader.GetInt32(1),
                HcpcsCode = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                PaymentNonFacility = (decimal)reader.GetDouble(4),
                PaymentFacility = (decimal)reader.GetDouble(5),
                Modifier = reader.IsDBNull(6) ? null : reader.GetString(6),
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
        cmd.CommandText = "SELECT DISTINCT year FROM pfs_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public void InsertFees(IEnumerable<PfsFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO pfs_fees (year, hcpcs_code, description, payment_non_facility, payment_facility, modifier, data_source, imported_at)
            VALUES (@year, @code, @desc, @non_fac, @fac, @modifier, @source, @imported_at)
            """;

        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pNonFac = cmd.Parameters.Add("@non_fac", SqliteType.Real);
        var pFac = cmd.Parameters.Add("@fac", SqliteType.Real);
        var pModifier = cmd.Parameters.Add("@modifier", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pYear.Value = fee.Year;
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pNonFac.Value = (double)fee.PaymentNonFacility;
            pFac.Value = (double)fee.PaymentFacility;
            pModifier.Value = (object?)fee.Modifier ?? DBNull.Value;
            pSource.Value = fee.DataSource;
            pImportedAt.Value = fee.ImportedAt.ToString("O");
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
