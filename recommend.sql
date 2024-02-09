-- drop view if exists Recommendations;

-- create view Recommendations as 
select t.Id, t.ShoppingListId, t.Name, count(*) as [Count], max(t.CheckDate) as [LastCheckDate], avg(t.diff) as [AvgDiff],
    JulianDay('now') - JulianDay(max(t.CheckDate)) as [Since],
    abs(avg(t.diff) - (JulianDay('now') - JulianDay(max(t.CheckDate)))) as [Weight],
    abs(avg(t.diff) - (JulianDay('now') - JulianDay(max(t.CheckDate)))) / count(*) as [Rank]
from (
    select i.Id, i.ShoppingListId, i.Name, h.CheckDate, (select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1) as prev_checkdate,
        JulianDay(h.CheckDate) - JulianDay((select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1)) as diff
    from ItemHistories as h
        inner join ShoppingListItems as i on i.id = h.ShoppingListItemId
    where i.Status = 3 -- only use items currently not active
)t
group by t.Id, t.ShoppingListId, t.Name
having count(*) > 2;
