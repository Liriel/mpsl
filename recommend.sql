-- This view assumes stored CheckDate values are UTC, since JulianDay('now') is evaluated in UTC.
-- It is a copy of the view created in Migrations/*_RecommendationOverdueRank.cs; keep the two
-- in sync if either changes.
--
-- Rank = -priority, so the ascending `orderby r.Rank` in ShoppingListController.GetRecommendations
-- lists the most promising item first. Weight = ratio = Since / typical interval.
--   typical interval = median gap between purchases (robust to a single holiday-sized gap), floored
--                      at 1 day so same-day re-purchases cannot blow the ratio up.
--   priority = ratio                 for ratio <= 2   (rises as the item comes due and goes overdue)
--            = 2 / (ratio - 1)       for ratio  > 2   (continuous at 2, then decays: 3x -> 1.0,
--                                                      5x -> 0.5, 10x -> 0.22 - probably no longer bought)
-- Items just bought (ratio ~0) rank last; due items (ratio ~1) and mildly overdue ones (1-3x) rank
-- first. The priority is scaled by Count / (Count + 5): a ratio from 3 gaps is noisy, from 90 it is not,
-- so staples beat rarities that merely happen to sit at 2x (checked against a prod snapshot: without
-- it the top 20 was all once-a-year items). Count > 2 and Status = 3 are unchanged; Kind = 0 keeps tasks out.

-- drop view if exists Recommendations;

-- create view Recommendations as
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
from r;
