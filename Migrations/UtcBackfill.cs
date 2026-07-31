using System.Globalization;
using System.Text;

namespace mps.Migrations
{
    /// <summary>
    /// Generates the SQL that backfills locally-stamped (Europe/Berlin) timestamps to UTC for the
    /// <c>UtcTimestamps</c> migration, and the reverse for its <c>Down()</c>.
    /// </summary>
    /// <remarks>
    /// Two tools that would normally do this job are unusable here: SQLite's <c>datetime(col,
    /// 'utc')</c> modifier resolves against the *applying machine's* timezone, and the containers
    /// set no <c>TZ</c>, so in production it would be a silent no-op. <c>TimeZoneInfo.
    /// FindSystemTimeZoneById("Europe/Berlin")</c> needs tzdata, which the Alpine image does not
    /// ship. So the EU DST rule - unchanged since 1996: DST runs from the last Sunday of March
    /// 02:00 local to the last Sunday of October 03:00 local - is computed with plain C# date
    /// arithmetic and emitted as SQL literals instead, covering years 1990-2100 so the generated
    /// <c>CASE</c> never needs revisiting.
    /// </remarks>
    public static class UtcBackfill
    {
        private const int FirstYear = 1990;
        private const int LastYear = 2100;

        /// <summary>
        /// Guard against shifting the <c>0001-01-01</c> sentinel some legacy rows were backfilled
        /// with (see <c>Migrations/20221003164317_AddDate.cs</c>): shifting it lands on year
        /// <c>0000</c>, which <see cref="DateTime"/> cannot parse back. The dev database has no such
        /// rows left, but production may.
        /// </summary>
        private const string MinValidValue = "1990-01-01";

        /// <summary>
        /// The last Sunday of March in <paramref name="year"/> - the local date DST begins on (at
        /// 02:00) under the EU rule.
        /// </summary>
        public static DateTime LastSundayOfMarch(int year) => LastSunday(year, 3);

        /// <summary>
        /// The last Sunday of October in <paramref name="year"/> - the local date DST ends on (at
        /// 03:00) under the EU rule.
        /// </summary>
        public static DateTime LastSundayOfOctober(int year) => LastSunday(year, 10);

        private static DateTime LastSunday(int year, int month)
        {
            var lastDayOfMonth = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            // DayOfWeek.Sunday == 0, so subtracting the enum's int value walks back to the Sunday
            // on or before the last day of the month.
            return lastDayOfMonth.AddDays(-(int)lastDayOfMonth.DayOfWeek);
        }

        /// <summary>
        /// Builds the forward (local Europe/Berlin -&gt; UTC) backfill for <paramref name="column"/>
        /// on <paramref name="table"/>. Rows stamped during CEST (last Sunday of March 02:00 local,
        /// up to but not including last Sunday of October 03:00 local) shift back 2 hours; everything
        /// else (CET) shifts back 1 hour. The ambiguous October hour - 02:00-03:00 local occurs twice
        /// at the changeover, once in each offset - is resolved as CEST, i.e. shifted -2 hours; worst
        /// case one hour of rows per year is off by an hour, irrelevant at this app's day/month
        /// granularity.
        /// </summary>
        public static string BuildForwardUpdate(string table, string column) =>
            BuildUpdate(table, column, summerStartHour: 2, summerEndHour: 3, summerShift: "-2 hours", otherShift: "-1 hours");

        /// <summary>
        /// Builds the reverse (UTC -&gt; local Europe/Berlin) backfill for <paramref name="column"/>
        /// on <paramref name="table"/>, mirroring <see cref="BuildForwardUpdate"/> with the DST
        /// boundaries expressed in UTC (last Sunday of March 01:00 UTC, up to but not including last
        /// Sunday of October 01:00 UTC) and the shifts inverted, so a rollback is possible.
        /// </summary>
        public static string BuildReverseUpdate(string table, string column) =>
            BuildUpdate(table, column, summerStartHour: 1, summerEndHour: 1, summerShift: "+2 hours", otherShift: "+1 hours");

        private static string BuildUpdate(
            string table,
            string column,
            int summerStartHour,
            int summerEndHour,
            string summerShift,
            string otherShift)
        {
            var summerTerms = new StringBuilder();
            for (int year = FirstYear; year <= LastYear; year++)
            {
                var start = LastSundayOfMarch(year).AddHours(summerStartHour);
                var end = LastSundayOfOctober(year).AddHours(summerEndHour);

                if (summerTerms.Length > 0)
                    summerTerms.Append(" or ");

                summerTerms
                    .Append('(').Append(column).Append(" >= '").Append(FormatLiteral(start)).Append('\'')
                    .Append(" and ").Append(column).Append(" < '").Append(FormatLiteral(end)).Append('\'')
                    .Append(')');
            }

            // datetime() truncates fractional seconds outright, which would break the lexicographic
            // TEXT comparison EF's SQLite provider relies on for e.g. `CheckDate < minDate` and
            // `orderby AddDate`. So the shift is computed on the truncated value and the original
            // fraction (substr from position 20, the '.fffffff' tail of a 'yyyy-MM-dd HH:mm:ss.fff...'
            // string - '' when the value has none) is re-appended. coalesce() falls back to the
            // untouched original value for anything datetime() cannot parse, rather than nulling it.
            return $@"update {table}
set {column} = coalesce(
        datetime({column}, case when {summerTerms} then '{summerShift}' else '{otherShift}' end)
            || substr({column}, 20),
        {column})
where {column} is not null and {column} >= '{MinValidValue}';
";
        }

        private static string FormatLiteral(DateTime value) =>
            value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
