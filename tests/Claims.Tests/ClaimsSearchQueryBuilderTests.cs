using Microsoft.Data.Sqlite;

namespace Claims.Tests;

// Unit tests for the SQL builder: user input must only ever reach the query as a bound parameter.
public class ClaimsSearchQueryBuilderTests
{
    private static SqliteConnection Conn() => Db.CreateSeeded();

    private static object? Param(SqliteCommand cmd, string name) => cmd.Parameters[name].Value;

    [Fact]
    public void Build_WithoutMember_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ClaimsSearchQueryBuilder().Build(Conn()));
    }

    [Fact]
    public void Build_Defaults_SortByDateDescAndFirstPage()
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).Build(Conn());

        Assert.Contains("ORDER BY submitted_at DESC, id DESC", cmd.CommandText);
        Assert.Equal("%%", Param(cmd, "@text"));
        Assert.Equal(0L, Param(cmd, "@offset"));
        Assert.Equal(ClaimsSearchQueryBuilder.PageSize, Param(cmd, "@limit"));
    }

    [Fact]
    public void Build_BindsMemberAsParameter()
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(42).Build(Conn());

        Assert.Contains("member_id = @member", cmd.CommandText);
        Assert.Equal(42, Param(cmd, "@member"));
    }

    [Theory]
    [InlineData("date", "asc", "ORDER BY submitted_at ASC, id ASC")]
    [InlineData("amount", "desc", "ORDER BY amount DESC, id DESC")]
    [InlineData("Status", "ASC", "ORDER BY status ASC, id ASC")]
    public void OrderBy_AllowedOptions_MapToFixedColumns(string sortBy, string sortDir, string expected)
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).OrderBy(sortBy, sortDir).Build(Conn());
        Assert.Contains(expected, cmd.CommandText);
    }

    [Fact]
    public void OrderBy_InvalidSortBy_ThrowsWithParamName()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ClaimsSearchQueryBuilder().OrderBy("provider", "asc"));
        Assert.Equal("sortBy", ex.ParamName);
    }

    [Fact]
    public void OrderBy_InvalidSortDir_ThrowsWithParamName()
    {
        var ex = Assert.Throws<ArgumentException>(() => new ClaimsSearchQueryBuilder().OrderBy("amount", "sideways"));
        Assert.Equal("sortDir", ex.ParamName);
    }

    [Fact]
    public void WithText_IsNeverPartOfTheSqlText()
    {
        const string payload = "' OR 1=1 --";
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).WithText(payload).Build(Conn());

        Assert.DoesNotContain(payload, cmd.CommandText);
        Assert.Equal("%" + payload + "%", Param(cmd, "@text"));
    }

    [Fact]
    public void WithText_EscapesLikeWildcardsAndEscapeChar()
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).WithText(@"a\b%c_d").Build(Conn());
        Assert.Equal(@"%a\\b\%c\_d%", Param(cmd, "@text"));
    }

    [Fact]
    public void WithText_Null_MatchesEverything()
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).WithText(null).Build(Conn());
        Assert.Equal("%%", Param(cmd, "@text"));
    }

    [Fact]
    public void WithText_Backslash_IsFoundLiterally()
    {
        var db = Conn();
        using (var insert = db.CreateCommand())
        {
            insert.CommandText = @"INSERT INTO claims VALUES (900, 1, 'Path Labs', 'Ref A\B', 10, 'paid', '2026-11-02')";
            insert.ExecuteNonQuery();
        }

        var ids = new ClaimsRepository(db).Search(1, @"A\B").Select(c => c.Id);
        Assert.Equal(new long[] { 900 }, ids);
    }

    [Theory]
    [InlineData(0, 0L)]
    [InlineData(1, 20L)]
    [InlineData(3, 60L)]
    [InlineData(-5, 0L)]
    public void Page_SetsOffset(int page, long expectedOffset)
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).Page(page).Build(Conn());
        Assert.Equal(expectedOffset, Param(cmd, "@offset"));
    }

    [Fact]
    public void Page_LargePage_DoesNotOverflow()
    {
        using var cmd = new ClaimsSearchQueryBuilder().ForMember(1).Page(int.MaxValue).Build(Conn());
        Assert.Equal((long)int.MaxValue * 20, Param(cmd, "@offset"));
    }

    [Fact]
    public void Build_AllUserValuesAreParameters()
    {
        using var cmd = new ClaimsSearchQueryBuilder()
            .ForMember(7).WithText("x").OrderBy("amount", "asc").Page(2).Build(Conn());

        var names = cmd.Parameters.Cast<SqliteParameter>().Select(p => p.ParameterName).OrderBy(n => n);
        Assert.Equal(new[] { "@limit", "@member", "@offset", "@text" }, names);
    }
}
