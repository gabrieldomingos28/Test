using Microsoft.Data.Sqlite;

namespace Claims.Tests;

// MYT-3108: tests for the product rules in FINDING.md.
// They use only the public ClaimsRepository/Db API, so they also run against the agent's version
// (check/agent_version) and show where that version is wrong.
public class MyTests
{
    private static ClaimsRepository Repo() => new(Db.CreateSeeded());

    private static long[] Ids(IEnumerable<Claim> claims) => claims.Select(c => c.Id).ToArray();

    // ---------- Search: "contains", any case ----------

    [Fact]
    public void Search_PartialText_FindsClaimsWhoseDescriptionContainsIt()
    {
        // FINDING.md: "dental" finds "Dental cleaning" and "Dental X-ray".
        Assert.Equal(new long[] { 101, 103 }, Ids(Repo().Search(1, "dental")).OrderBy(x => x));
    }

    [Fact]
    public void Search_IsCaseInsensitive()
    {
        Assert.Equal(new long[] { 101, 103 }, Ids(Repo().Search(1, "DENTAL")).OrderBy(x => x));
    }

    [Fact]
    public void Search_MatchesProvider()
    {
        Assert.Equal(new long[] { 104, 105 }, Ids(Repo().Search(1, "pharmacy")).OrderBy(x => x));
    }

    [Fact]
    public void Search_NameWithApostrophe_IsFound()
    {
        // FINDING.md: O'Brien finds "O'Brien Eye Clinic".
        Assert.Equal(new long[] { 102 }, Ids(Repo().Search(1, "O'Brien")));
    }

    [Fact]
    public void Search_PercentSign_IsMatchedLiterally()
    {
        // "100%" must not act as a wildcard and also match "Copay 100 dollars".
        Assert.Equal(new long[] { 104 }, Ids(Repo().Search(1, "100%")));
    }

    [Fact]
    public void Search_Underscore_IsMatchedLiterally()
    {
        // "_" is a single-character wildcard in LIKE; no claim contains a real underscore.
        Assert.Empty(Repo().Search(1, "_"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Search_NoText_ReturnsAllClaimsOfTheMember(string? text)
    {
        Assert.Equal(new long[] { 101, 102, 103, 104, 105 }, Ids(Repo().Search(1, text)).OrderBy(x => x));
    }

    // ---------- Member isolation / injection in the search text ----------

    [Fact]
    public void Search_NeverReturnsOtherMembersClaims()
    {
        // "crown" only exists for member 2.
        Assert.Empty(Repo().Search(1, "crown"));
        Assert.All(Repo().Search(1, ""), c => Assert.True(c.Id < 200));
    }

    [Theory]
    [InlineData("' OR 1=1 --")]
    [InlineData("%' OR member_id = 2 --")]
    [InlineData("\") OR 1=1 --")]
    public void Search_InjectionInText_IsTreatedAsPlainText(string text)
    {
        Assert.Empty(Repo().Search(1, text));
    }

    // ---------- Sorting ----------

    [Fact]
    public void Search_SortByAmountAsc()
    {
        Assert.Equal(new long[] { 104, 105 }, Ids(Repo().Search(1, "City Pharmacy", "amount", "asc")));
    }

    [Fact]
    public void Search_SortByDateDesc()
    {
        Assert.Equal(new long[] { 103, 101 }, Ids(Repo().Search(1, "Bright Smiles Dental", "date", "desc")));
    }

    [Fact]
    public void Search_SortByStatusAsc()
    {
        Assert.Equal(new long[] { 105, 104 }, Ids(Repo().Search(1, "City Pharmacy", "status", "asc")));
    }

    [Fact]
    public void Search_DefaultSort_IsDateDescending()
    {
        Assert.Equal(new long[] { 105, 104, 103, 102, 101 }, Ids(Repo().Search(1, "")));
    }

    [Fact]
    public void Search_NullSortOptions_UseDefaults()
    {
        Assert.Equal(new long[] { 105, 104, 103, 102, 101 }, Ids(Repo().Search(1, "", null, null)));
    }

    [Fact]
    public void Search_SortOptions_AreCaseInsensitive()
    {
        Assert.Equal(new long[] { 104, 105 }, Ids(Repo().Search(1, "City Pharmacy", "AMOUNT", "ASC")));
    }

    [Theory]
    [InlineData("submitted_at")]                        // real column, but not an allowed option
    [InlineData("id")]
    [InlineData("")]
    [InlineData("amount, (SELECT name FROM members)")]
    [InlineData("amount;DROP TABLE claims")]
    [InlineData("(CASE WHEN 1=1 THEN amount ELSE id END)")]
    public void Search_SortByNotAllowed_ThrowsArgumentException(string sortBy)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", sortBy, "asc"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("up")]
    [InlineData("asc, (SELECT name FROM members)")]
    public void Search_SortDirNotAllowed_ThrowsArgumentException(string sortDir)
    {
        Assert.Throws<ArgumentException>(() => Repo().Search(1, "City Pharmacy", "amount", sortDir));
    }

    // ---------- Paging: 20 per page ----------

    [Fact]
    public void Search_ReturnsPagesOf20()
    {
        var db = Db.CreateSeeded();
        AddClaims(db, memberId: 3, count: 45);
        var repo = new ClaimsRepository(db);

        Assert.Equal(20, repo.Search(3, "", page: 0).Count);
        Assert.Equal(20, repo.Search(3, "", page: 1).Count);
        Assert.Equal(5, repo.Search(3, "", page: 2).Count);
        Assert.Empty(repo.Search(3, "", page: 3));
    }

    [Fact]
    public void Search_Pages_DoNotOverlap()
    {
        var db = Db.CreateSeeded();
        AddClaims(db, memberId: 3, count: 45);
        var repo = new ClaimsRepository(db);

        var all = Enumerable.Range(0, 3).SelectMany(p => Ids(repo.Search(3, "", "amount", "asc", p))).ToList();
        Assert.Equal(45, all.Count);
        Assert.Equal(45, all.Distinct().Count());
    }

    [Fact]
    public void Search_NegativePage_ReturnsFirstPage()
    {
        Assert.Equal(Ids(Repo().Search(1, "", page: 0)), Ids(Repo().Search(1, "", page: -1)));
    }

    private static void AddClaims(SqliteConnection db, int memberId, int count)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO claims VALUES (@id, @member, 'Test Provider', 'Test claim', @amount, 'paid', '2026-11-01')";
        var id = cmd.Parameters.Add("@id", SqliteType.Integer);
        cmd.Parameters.AddWithValue("@member", memberId);
        var amount = cmd.Parameters.Add("@amount", SqliteType.Real);
        for (var i = 0; i < count; i++)
        {
            id.Value = 1000 + i;
            amount.Value = i % 3;   // many ties, to prove paging stays stable
            cmd.ExecuteNonQuery();
        }
    }
}
