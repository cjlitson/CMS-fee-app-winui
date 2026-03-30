using CMSFeeApp.Core.Models;
using Microsoft.Data.Sqlite;

namespace CMSFeeApp.Data.Repositories;

public class DmeposRepository
{
    private readonly DatabaseContext _context;

    public DmeposRepository(DatabaseContext context)
    {
        _context = context;
    }

    public IReadOnlyList<DmepsFee> GetFees(int year, string? stateAbbr = null, string? hcpcsCode = null, string? descriptionKeyword = null)
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();

        var conditions = new List<string> { "year = @year" };
        cmd.Parameters.AddWithValue("@year", year);

        if (!string.IsNullOrWhiteSpace(stateAbbr))
        {
            conditions.Add("state_abbr = @state");
            cmd.Parameters.AddWithValue("@state", stateAbbr.ToUpperInvariant());
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

        cmd.CommandText = $"SELECT id, hcpcs_code, description, state_abbr, year, allowable, modifier, data_source, imported_at FROM dmepos_fees WHERE {string.Join(" AND ", conditions)} ORDER BY hcpcs_code, state_abbr LIMIT 5000";

        var results = new List<DmepsFee>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new DmepsFee
            {
                Id = reader.GetInt32(0),
                HcpcsCode = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                StateAbbr = reader.GetString(3),
                Year = reader.GetInt32(4),
                Allowable = (decimal)reader.GetDouble(5),
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
        cmd.CommandText = "SELECT DISTINCT year FROM dmepos_fees ORDER BY year DESC";

        var years = new List<int>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            years.Add(reader.GetInt32(0));
        return years;
    }

    public IReadOnlyList<string> GetAvailableStates()
    {
        var connection = _context.GetConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT state_abbr FROM dmepos_fees ORDER BY state_abbr";

        var states = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            states.Add(reader.GetString(0));
        return states;
    }

    public void InsertFees(IEnumerable<DmepsFee> fees)
    {
        var connection = _context.GetConnection();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO dmepos_fees (hcpcs_code, description, state_abbr, year, allowable, modifier, data_source, imported_at)
            VALUES (@code, @desc, @state, @year, @allowable, @modifier, @source, @imported_at)
            """;

        var pCode = cmd.Parameters.Add("@code", SqliteType.Text);
        var pDesc = cmd.Parameters.Add("@desc", SqliteType.Text);
        var pState = cmd.Parameters.Add("@state", SqliteType.Text);
        var pYear = cmd.Parameters.Add("@year", SqliteType.Integer);
        var pAllowable = cmd.Parameters.Add("@allowable", SqliteType.Real);
        var pModifier = cmd.Parameters.Add("@modifier", SqliteType.Text);
        var pSource = cmd.Parameters.Add("@source", SqliteType.Text);
        var pImportedAt = cmd.Parameters.Add("@imported_at", SqliteType.Text);

        foreach (var fee in fees)
        {
            pCode.Value = fee.HcpcsCode;
            pDesc.Value = (object?)fee.Description ?? DBNull.Value;
            pState.Value = fee.StateAbbr;
            pYear.Value = fee.Year;
            pAllowable.Value = (double)fee.Allowable;
            pModifier.Value = (object?)fee.Modifier ?? DBNull.Value;
            pSource.Value = fee.DataSource;
            pImportedAt.Value = fee.ImportedAt.ToString("O");
            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
    }
}
