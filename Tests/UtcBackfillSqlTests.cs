using Microsoft.Data.Sqlite;
using mps.Migrations;
using Xunit;

namespace Tests;

/// <summary>
/// F7: pins the SQL <see cref="UtcBackfill"/> generates for the <c>UtcTimestamps</c> migration -
/// the DST boundary arithmetic, the fractional-second-preserving shift, and the guards that keep
/// the sentinel row and <c>PushSubscriptions</c> untouched. Pure unit tests: no host, no
/// <c>TestAppFactory</c>, just an in-memory SQLite connection.
/// </summary>
public class UtcBackfillSqlTests
{
    /// <summary>
    /// The EU DST rule is "last Sunday of March/October", not a fixed date, so the boundary
    /// arithmetic in <see cref="UtcBackfill.LastSundayOfMarch"/>/<see cref="UtcBackfill.LastSundayOfOctober"/>
    /// is pinned against hardcoded known dates rather than <c>TimeZoneInfo.FindSystemTimeZoneById</c>
    /// - the whole point of generating the SQL in C# is to have no tzdata dependency, so the test
    /// must not reintroduce one.
    /// </summary>
    [Theory]
    [InlineData(2022, 3, 27)]
    [InlineData(2022, 10, 30)]
    [InlineData(2023, 3, 26)]
    [InlineData(2023, 10, 29)]
    [InlineData(2024, 3, 31)]
    [InlineData(2024, 10, 27)]
    [InlineData(2025, 3, 30)]
    [InlineData(2025, 10, 26)]
    [InlineData(2026, 3, 29)]
    [InlineData(2026, 10, 25)]
    public void LastSunday_MatchesKnownEuDstDates(int year, int month, int expectedDay)
    {
        var result = month == 3 ? UtcBackfill.LastSundayOfMarch(year) : UtcBackfill.LastSundayOfOctober(year);

        Assert.Equal(new DateTime(year, month, expectedDay), result);
        Assert.Equal(DayOfWeek.Sunday, result.DayOfWeek);
    }

    /// <summary>
    /// Runs the generated forward-update SQL against a real SQLite engine (not a hand-rolled
    /// re-implementation) and pins the behaviour the migration depends on: summer rows shift back
    /// 2 hours, winter rows shift back 1 hour, fractional seconds survive the shift byte-for-byte
    /// (datetime() truncates them, so the SQL has to re-append the original tail), a NULL is left
    /// alone, and the 0001-01-01 sentinel (see Migrations/20221003164317_AddDate.cs) is left alone
    /// because shifting it would land on an unparseable year 0000.
    /// </summary>
    [Fact]
    public void BuildForwardUpdate_ShiftsRowsByTheCorrectDstOffset()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        Execute(connection, "create table T (D text)");
        InsertRow(connection, "2025-07-14 14:53:45.4336209"); // CEST -> -2h
        InsertRow(connection, "2025-01-15 12:00:00.1");       // CET -> -1h
        InsertRow(connection, "2025-03-30 01:59:59");         // last second before the spring change (still CET)
        InsertRow(connection, "2025-03-30 03:00:00");         // first second of CEST
        InsertRow(connection, "2025-06-01 10:00:00");         // no fractional part
        InsertRow(connection, "0001-01-01 00:00:00.0000000"); // sentinel - must not move
        InsertRow(connection, null);                          // NULL - must not move

        Execute(connection, UtcBackfill.BuildForwardUpdate("T", "D"));

        var results = SelectAllOrdered(connection);

        Assert.Equal("2025-07-14 12:53:45.4336209", results[0]);
        Assert.Equal("2025-01-15 11:00:00.1", results[1]);
        Assert.Equal("2025-03-30 00:59:59", results[2]);
        Assert.Equal("2025-03-30 01:00:00", results[3]);
        Assert.Equal("2025-06-01 08:00:00", results[4]);
        Assert.Equal("0001-01-01 00:00:00.0000000", results[5]);
        Assert.Null(results[6]);
    }

    /// <summary>
    /// A forward shift followed by the mirrored reverse shift must restore the original values, so
    /// a rollback (<c>UtcTimestamps.Down</c>) is actually safe. Skips the ambiguous October
    /// changeover hour on purpose - by design one hour of rows a year cannot round-trip (resolved as
    /// CEST both ways), which is documented and accepted in <see cref="UtcBackfill"/>.
    /// </summary>
    [Fact]
    public void ForwardThenReverse_RestoresTheOriginalValues()
    {
        string[] originals =
        [
            "2025-07-14 14:53:45.4336209",
            "2025-01-15 12:00:00.1",
            "2025-03-30 01:59:59",
            "2025-03-30 03:00:00",
            "2025-06-01 10:00:00",
        ];

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        Execute(connection, "create table T (D text)");
        foreach (var value in originals)
            InsertRow(connection, value);

        Execute(connection, UtcBackfill.BuildForwardUpdate("T", "D"));
        Execute(connection, UtcBackfill.BuildReverseUpdate("T", "D"));

        var results = SelectAllOrdered(connection);
        Assert.Equal(originals, results);
    }

    /// <summary>
    /// PushSubscriptions.CreatedAt has always been DateTime.UtcNow, so the migration must not touch
    /// it - pins that neither generated statement mentions the table.
    /// </summary>
    [Fact]
    public void GeneratedSql_NeverMentionsPushSubscriptions()
    {
        var forward = string.Concat(
            UtcBackfill.BuildForwardUpdate("ShoppingListItems", "AddDate"),
            UtcBackfill.BuildForwardUpdate("ShoppingListItems", "CheckDate"),
            UtcBackfill.BuildForwardUpdate("ShoppingListItems", "LastChangedDate"),
            UtcBackfill.BuildForwardUpdate("ItemHistories", "CheckDate"));

        var reverse = string.Concat(
            UtcBackfill.BuildReverseUpdate("ShoppingListItems", "AddDate"),
            UtcBackfill.BuildReverseUpdate("ShoppingListItems", "CheckDate"),
            UtcBackfill.BuildReverseUpdate("ShoppingListItems", "LastChangedDate"),
            UtcBackfill.BuildReverseUpdate("ItemHistories", "CheckDate"));

        Assert.DoesNotContain("PushSubscriptions", forward);
        Assert.DoesNotContain("PushSubscriptions", reverse);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void InsertRow(SqliteConnection connection, string? value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "insert into T (D) values ($d)";
        command.Parameters.AddWithValue("$d", (object?)value ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static List<string?> SelectAllOrdered(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "select D from T order by rowid";
        using var reader = command.ExecuteReader();

        var results = new List<string?>();
        while (reader.Read())
            results.Add(reader.IsDBNull(0) ? null : reader.GetString(0));

        return results;
    }
}
