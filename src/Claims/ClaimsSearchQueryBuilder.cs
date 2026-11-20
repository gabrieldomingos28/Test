using System.Text;
using Microsoft.Data.Sqlite;

namespace Claims;

// Builds the claims search command.
// Safety rules:
//   - Every user-supplied value is bound as a parameter.
//   - Identifiers and keywords (ORDER BY column, ASC/DESC) come only from fixed allowlists,
//     because they cannot be bound as parameters. User input never reaches the SQL text.
public sealed class ClaimsSearchQueryBuilder
{
    public const int PageSize = 20;

    private const string SelectClause =
        "SELECT id, provider, description, amount, status, submitted_at FROM claims";

    private const string DefaultSortColumn = "submitted_at";

    // Public sort key -> SQL column. Product rule (FINDING.md): only date, amount and status.
    private static readonly IReadOnlyDictionary<string, string> SortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"]   = "submitted_at",
            ["amount"] = "amount",
            ["status"] = "status",
        };

    // Public sort direction -> SQL keyword.
    private static readonly IReadOnlyDictionary<string, string> SortDirections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["asc"]  = "ASC",
            ["desc"] = "DESC",
        };

    private int? _memberId;
    private string _text = "";
    private string _sortColumn = DefaultSortColumn;
    private string _sortDirection = "DESC";
    private long _offset;

    public ClaimsSearchQueryBuilder ForMember(int memberId)
    {
        _memberId = memberId;
        return this;
    }

    // Matches the text anywhere in description or provider. LIKE wildcards in the input are matched literally.
    public ClaimsSearchQueryBuilder WithText(string? text)
    {
        _text = text ?? "";
        return this;
    }

    // sortBy: date | amount | status (null = date). sortDir: asc | desc (null = desc).
    // Anything else throws ArgumentException; it is never written into the SQL.
    public ClaimsSearchQueryBuilder OrderBy(string? sortBy, string? sortDir)
    {
        if (!SortColumns.TryGetValue(sortBy ?? "date", out var column))
            throw new ArgumentException($"Invalid sort option. Allowed: {string.Join(", ", SortColumns.Keys)}.", nameof(sortBy));
        if (!SortDirections.TryGetValue(sortDir ?? "desc", out var direction))
            throw new ArgumentException($"Invalid sort direction. Allowed: {string.Join(", ", SortDirections.Keys)}.", nameof(sortDir));

        _sortColumn = column;
        _sortDirection = direction;
        return this;
    }

    // Zero-based page; negative values are treated as 0.
    public ClaimsSearchQueryBuilder Page(int page)
    {
        _offset = Math.Max(page, 0) * (long)PageSize;
        return this;
    }

    public SqliteCommand Build(SqliteConnection connection)
    {
        if (_memberId is null)
            throw new InvalidOperationException("ForMember must be called: searches are always scoped to one member.");

        var sql = new StringBuilder(SelectClause)
            .Append(" WHERE member_id = @member")
            .Append(@" AND (description LIKE @text ESCAPE '\' OR provider LIKE @text ESCAPE '\')")
            .Append(" ORDER BY ").Append(_sortColumn).Append(' ').Append(_sortDirection)
            .Append(", id ").Append(_sortDirection)                 // stable paging on ties
            .Append(" LIMIT @limit OFFSET @offset");

        var cmd = connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("@member", _memberId.Value);
        cmd.Parameters.AddWithValue("@text", "%" + EscapeLike(_text) + "%");
        cmd.Parameters.AddWithValue("@limit", PageSize);
        cmd.Parameters.AddWithValue("@offset", _offset);
        return cmd;
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
