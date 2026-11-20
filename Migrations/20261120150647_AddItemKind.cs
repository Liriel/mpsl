using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace mps.Migrations
{
    /// <inheritdoc />
    public partial class AddItemKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "ShoppingListItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // Backfill by NAME (never ids): the classifier rules of ItemKindClassifier in SQL, plus the
            // known production errands / shop reminders. Dm and Apotheke are also list names, which the
            // "named like another list" rule catches where the lists exist. The match key is
            // approximated: lower case, umlauts spelled out, ss folding, spaces/dashes/dots dropped.
            migrationBuilder.Sql(@"update ShoppingListItems as i set Kind = 1
where replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%packen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%tanken%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%abholen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%bringen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%anrufen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%zahlen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%termin%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%erledigen%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%schularbeit%'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%zeiten'
   or replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') like '%plaene'
   or exists (select 1 from ShoppingLists as l where l.Id <> i.ShoppingListId and replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(l.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') <> '' and replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(l.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss') = replace(replace(replace(replace(lower(replace(replace(replace(replace(replace(replace(replace(i.Name,'Ä','ae'),'Ö','oe'),'Ü','ue'),'ä','ae'),'ö','oe'),'ü','ue'),'ß','ss')),' ',''),'-',''),'.',''),'sz','ss'))
   or lower(i.Name) in ('hortzeiten', 'kidspacken', 'gutscheineinpacken', 'englishschularbeit', 'stundenpläne', 'tanken', 'dm', 'apotheke', 'c&a', 'pagro', 'clever', 'sonnentor');");

            // tasks are not purchases
            migrationBuilder.Sql(@"delete from ItemHistories where ShoppingListItemId in (select Id from ShoppingListItems where Kind = 1);");

            // same view as RecommendationOverdueRank plus the Kind = 0 filter (mirrored in recommend.sql)
            migrationBuilder.Sql("drop view if exists Recommendations;");
            migrationBuilder.Sql(@"create view Recommendations as
with t as (
    select i.Id, i.ShoppingListId, i.Name, h.CheckDate,
        JulianDay(h.CheckDate) - JulianDay((select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1)) as diff
    from ItemHistories as h
        inner join ShoppingListItems as i on i.id = h.ShoppingListItemId
    where i.Status = 3 -- only use items currently not active
        and i.Kind = 0 -- products only: a task is not bought
),
o as (
    select Id, diff, row_number() over (partition by Id order by diff) as rn, count(*) over (partition by Id) as n
    from t
    where diff is not null
),
med as (
    select Id, avg(diff) as Median from o where rn in ((n + 1) / 2, (n + 2) / 2) group by Id
),
agg as (
    select t.Id, t.ShoppingListId, t.Name, count(*) as [Count], max(t.CheckDate) as [LastCheckDate], avg(t.diff) as [AvgDiff],
        JulianDay('now') - JulianDay(max(t.CheckDate)) as [Since]
    from t
    group by t.Id, t.ShoppingListId, t.Name
    having count(*) > 2
),
r as (
    select agg.*, agg.Since / max(med.Median, 1.0) as Ratio
    from agg inner join med on med.Id = agg.Id
)
select Id, ShoppingListId, Name, [Count], [LastCheckDate], [AvgDiff], [Since],
    Ratio as [Weight],
    -(case when Ratio <= 2.0 then Ratio else 2.0 / (Ratio - 1.0) end) * [Count] / ([Count] + 5.0) as [Rank]
from r;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // the view must go before the column it reads; the deleted task history is not restored
            migrationBuilder.Sql("drop view if exists Recommendations;");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "ShoppingListItems");

            // definition of RecommendationOverdueRank, verbatim
            migrationBuilder.Sql(@"create view Recommendations as
with t as (
    select i.Id, i.ShoppingListId, i.Name, h.CheckDate,
        JulianDay(h.CheckDate) - JulianDay((select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1)) as diff
    from ItemHistories as h
        inner join ShoppingListItems as i on i.id = h.ShoppingListItemId
    where i.Status = 3 -- only use items currently not active
),
o as (
    select Id, diff, row_number() over (partition by Id order by diff) as rn, count(*) over (partition by Id) as n
    from t
    where diff is not null
),
med as (
    select Id, avg(diff) as Median from o where rn in ((n + 1) / 2, (n + 2) / 2) group by Id
),
agg as (
    select t.Id, t.ShoppingListId, t.Name, count(*) as [Count], max(t.CheckDate) as [LastCheckDate], avg(t.diff) as [AvgDiff],
        JulianDay('now') - JulianDay(max(t.CheckDate)) as [Since]
    from t
    group by t.Id, t.ShoppingListId, t.Name
    having count(*) > 2
),
r as (
    select agg.*, agg.Since / max(med.Median, 1.0) as Ratio
    from agg inner join med on med.Id = agg.Id
)
select Id, ShoppingListId, Name, [Count], [LastCheckDate], [AvgDiff], [Since],
    Ratio as [Weight],
    -(case when Ratio <= 2.0 then Ratio else 2.0 / (Ratio - 1.0) end) * [Count] / ([Count] + 5.0) as [Rank]
from r;");
        }
    }
}
