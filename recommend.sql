select *, q.weight - avg_diff as m from (
    select t.Name, count(*) as [cnt], max(t.CheckDate) as [last_checkdate], avg(t.diff) as [avg_diff], 
        case when JulianDay('now') > JulianDay(max(t.CheckDate)) + avg(t.diff) then 1 else 0 end as [recommend],
        JulianDay('now') - JulianDay(max(t.CheckDate)) + avg(t.diff) as weight
    from (
        select i.Id, i.name, h.CheckDate, (select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1) as prev_checkdate,
            JulianDay(h.CheckDate) - JulianDay((select CheckDate from ItemHistories where ShoppingListItemId = i.Id and CheckDate < h.CheckDate order by CheckDate desc limit 1)) as diff
        from ItemHistories as h
            inner join ShoppingListItems as i on i.id = h.ShoppingListItemId
    )t
    group by t.Name 
)q
where q.recommend = 1
order by q.weight - avg_diff, [cnt] desc