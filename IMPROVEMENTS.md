# Improvements from the production data analysis

Analysis of the production database (snapshot pulled from corpi on 2026-11-20; schema at `AddPushSubscriptions`, `UtcTimestamps` still pending). 9 lists, 969 items, 6,004 history rows, 67 units, 6 users, 8 push subscriptions. Items keep their ids (I0–I8) when completed, like `FINDINGS.md`.

**Cleanup rule (stated once, up front):** a cleanup with only one correct answer goes in an
**EF migration**, so it runs automatically on deploy. A cleanup that needs a person to decide (which
of two items survives) goes through the **merge UI from I5**. Neither needs manual SQL on corpi.

## I0 — Deploy the pending `UtcTimestamps` migration first
- It was dry-run against a copy of prod: it applies cleanly, every timestamp moves by −1 h or −2 h,
  and there are no pre-1990 sentinel rows.
- It goes first because the new migrations below must stack on top of it, and the unique index in
  I1 compares `CheckDate` values.

## I1 — Duplicate ItemHistory rows from the archive race (closes FINDINGS F4) — DONE
- **Status:** DONE: `IItemArchiveService` claims rows atomically; migration `ArchiveRaceNameTrimAndPushEndpoints` removes the 18 duplicates and adds the unique index.
- **Evidence:** 18 pairs with identical `(ShoppingListItemId, CheckDate)` on 2026-04-17 (ids 5233–5250
  and 5251–5268).
- **Cause:** `ShoppingListController.GetItems` archives items and writes history inside a GET. That
  is read, then modify, then save, with no guard. Concurrent refreshes (window focus, two devices)
  both write history.
- **Fix:** move archiving into a small `IItemArchiveService.ArchiveStale(listId)` that **claims**
  rows atomically: `ExecuteUpdate(set Status=Archived where Id in (...) and Status==Checked and
  CheckDate<cutoff)`, then writes history only for the ids that call actually updated. The simplest
  correct form: select the candidate ids, then for each run `ExecuteUpdate … where Id==x &&
  Status==Checked` and insert history only when it returns 1. `GetItems` calls the service, so the
  client contract stays the same. Optional follow-up: also run it from a hosted timer, so lists
  nobody opens get archived too.
- **Data:** a migration that deletes the duplicate rows (keeping the lowest Id per
  `(ShoppingListItemId, CheckDate)`), then adds a **unique index** on that pair as a backstop. The
  index lives on the `ItemHistory` model config, so the snapshot stays in sync.
- **Tests:** two concurrent `GetItems` calls against one checked item produce exactly one history
  row, and the unique index rejects a duplicate insert.

## I2 — Untrimmed names get past duplicate detection — DONE
- **Status:** DONE: trimmed/collapsed in `NameHelper` and the AutoMapper maps; same migration trims names and units and merges collisions (#828 into #48).
- **Evidence:** `'Klopapier '` (#828, 14 purchases) exists next to `'Klopapier'` (#48, 71 purchases).
  The unit table has the same twins: `'750g'`/`'750g '` and `'.'`/`'. '`.
- **Cause:** `NameHelper.GetNormalizedName` (`Infrastructure/NameHelper.cs:42`) doesn't trim, and
  nothing trims `Name` either. The AutoMapper maps in `Infrastructure/AutomapperConfiguration.cs:26,34`
  copy the name as-is. `UnitService.GetOrCreateUnit` already trims new input, but the old rows are
  untrimmed.
- **Fix:** trim and collapse internal whitespace in `GetNormalizedName`. Trim `Name` in both AutoMapper
  maps (add and edit), plus `[Required]`/whitespace validation so a name of only spaces gets a 400.
  The lookup in `AddItem` (`ShoppingListController.cs:138`) is then correct as written.
- **Data:** a migration that trims `Name` and recomputes `NormalizedName`. Where trimming collides
  with an existing item (the Klopapier case), merge them: move the history onto the older row,
  delete the newer one, and keep the older one's status if it is open. For units, trim them and
  repoint `ShoppingListItems.UnitId`/`ItemHistories.UnitId` from each twin to the surviving unit.
  The merge SQL is the same as I5's server-side merge, so share the logic, or write it out in the
  migration with a comment linking to it.
- **Tests:** adding `"Milch "` reuses `"Milch"`, and a whitespace-only name returns 400.

## I3 — Dead push subscriptions are never removed — DONE
- **Status:** DONE: `.invalid` endpoints are unsubscribed before sending, `SubscribeAsync` looks up by endpoint; same migration deletes dead/duplicate rows and adds the unique index.
- **Evidence:** 4 of 8 rows point at `permanently-removed.invalid` (Chrome's marker for retired
  FCM subscriptions), and one endpoint is stored twice. All 8 belong to the admin account.
- **Cause:** `WebPushNotificationService.SendToSubscriptionAsync` (`Services/WebPushNotificationService.cs:306`)
  only removes a subscription on 410/404. A `.invalid` host fails at DNS lookup (`HttpRequestException`,
  no status code), which the generic `catch` just logs.
- **Fix:** also unsubscribe when the endpoint host ends in `.invalid`, which can be checked before
  sending. A plain transient network error should **not** remove a subscription. Optionally keep a
  failure counter and remove after N consecutive failures (this needs a column, so skip it unless it
  is wanted). In `PushSubscriptionService.SubscribeAsync` (`Services/PushSubscriptionService.cs:35`),
  look the subscription up by `Endpoint` alone, so a re-subscribe updates the existing row instead
  of adding a new one.
- **Data:** a migration that deletes `.invalid` endpoints and duplicate endpoints (keeping the newest
  row), then adds a **unique index on `Endpoint`**.
- **Tests:** an extended `Tests/WebPushNotificationTests.cs`, where the recording fake throws a
  DNS-style exception for a `.invalid` endpoint and the test asserts the row is removed. A transient
  error leaves the row in place.

## I4 — Typos and near-duplicates at entry time — DONE
- **Status:** DONE: `NameHelper.GetMatchKey` + `IItemMatchService` (Services/ItemMatchService.cs), `SearchItems` matches the key, `GET {id}/suggest` + `ItemSuggestionDialogComponent` on the add form, `RemoveItem` deletes fresh history-less items. No migration. See CLAUDE.md for the exact rules.
- **Evidence:** about 70 genuine near-duplicate pairs in Einkaufsliste: typos (Mozarella, Jogurt,
  Gauda, Schampoo), singular/plural (Banane/Bananen, Semmel/Semmeln), ß/ss (Weißwein/Weisswein),
  spacing (Wiener wurst, PassTomaten). Also 19 items archived without ever being bought, most of
  them created and removed within seconds (Würsrl, Frischkäsw, kuchenr).
- **Fix, part a, normalization:** extend the comparison key, not the stored `NormalizedName`. Add a
  separate `NameHelper.GetMatchKey(name)` that maps ß→ss and drops spaces and punctuation. Leave
  `NormalizedName` as it is, because search and the uniqueness lookup depend on it. Then
  `SearchItems` (`ShoppingListController.cs:211`) also matches on the key, so typing "passtomaten"
  finds "Pass Tomaten".
- **Fix, part b, suggest instead of create:** when the add form's name has no exact match, the server
  returns the closest existing items (edit distance ≤ 2 on the match key, or the same key with a
  plural suffix stripped). The client shows them as a "Meintest du …?" choice in the existing
  `mat-autocomplete` (`shopping-list.component.html:104`), or in a snackbar action after the add.
  Rank by purchase count, so the established spelling wins. Do it in memory: one list has about 825
  rows, so loading `(Id, Name, Count)` per add is cheap.
- **Fix, part c, typo removal leaves nothing behind:** in `RemoveItem` (`ShoppingListController.cs:193`),
  if the item has no history and `AddDate` is less than about 10 minutes ago, **delete** it instead
  of archiving it. It goes through `IEntityService.Delete` so the SignalR removal broadcast still
  runs.
- **Tests:** the match key collapses the listed variants. A suggestion is returned for "Mozarela".
  Removing a fresh item with no history deletes it, and removing an old one archives it.

## I5 — Merge items (cleanup tool for existing duplicates) — DONE
- **Status:** DONE: `POST {id}/merge`, `GET {id}/duplicates`, `IItemMergeService`, "Merge duplicates" panel in the declutter page. On the prod copy list 1 yields 63 groups, about 6 of them with a false positive (distance-2 pairs such as Duschgel/Waschgel); sources can be unticked per group. No migration.
- **Fix:** add `POST /api/ShoppingList/{id}/merge` with body `{ targetId, sourceIds[] }`, admin-only
  or `ApprovedUser`. It goes through `CanAccessList`, and every id must belong to the list. It
  repoints `ItemHistories.ShoppingListItemId` to the target, keeps the more active status (Open
  beats Checked beats Archived), keeps the target's name, unit and hint (copying the source's hint
  if the target has none), deletes the sources through `DeleteRange` so the removal broadcasts run,
  and saves once. Errors use the standard 400 shape.
- **UI:** a "Merge duplicates" section in the existing `DeclutterComponent`
  (`ClientApp/src/app/declutter/`). It lists suggested groups from the same matcher as I4, and you
  pick the survivor. The I4 suggestion logic should live in one service (e.g. `ItemMatchService`)
  so the add-time suggestion and the merge groups agree.
- **Tests:** merging moves the history and sums the counts, a cross-list id gets 400, and an
  unauthorized caller gets 403.

## I6 — Unit field used for amounts, and junk units — DONE
- **Status:** DONE: `UnitParser` splits quantities on add/edit, `GetOrCreateUnit` returns an `EntityOperationResult` and rejects letterless new units (400 on `UnitShortName`; `EntityController.PostMapping` can now return a `ValidationError`), the unit search endpoint filters server-side (long name or used 3+ times, so no client change), migration `UnitCleanup` cleans the data (prod copy: units 67 -> 24; 13 items and 132 history rows got an amount split out). Kept canonical/named units even when unreferenced; `0p` stays (a 0 prefix is not a quantity).
- **Evidence:** 61 of 67 units have no long name. Many are quantities or junk (`1kg`, `500g`, `2x`,
  `6er`, `10`, `.`, `...`, `&`, `Poo`, `Svhla`). 178 history rows have a unit but no amount.
- **Cause:** `UnitService.GetOrCreateUnit` (`Services/UnitService.cs:17`) creates any non-empty string
  as a unit.
- **Fix:**
  - On input (in `AddItem` and `ItemController.PostMapping`, before `GetOrCreateUnit`): parse
    `^(\d+)\s*([a-zA-Zäöü]*)$`. `"500g"` becomes amount 500 + unit `g`, `"2x"`/`"3"` becomes amount
    only, and `"6er"` becomes amount 6. Only do this when the amount field is empty.
  - In `GetOrCreateUnit`: reject a short name that is only digits or punctuation (400 on the unit
    field via the existing `ServerValidation` path). Free-text new units can stay, since the input
    is limited to 5 characters anyway.
  - Client: the unit autocomplete (`shopping-list.component.ts:151`) offers only units that have a
    long name, or that are used at least N times.
- **Data:** a migration that (a) splits numeric-prefixed units on `ShoppingListItems` and
  `ItemHistories` into amount + unit where the amount is null, (b) repoints the punctuation and
  digit-only units to null, (c) deletes units nobody references anymore, and (d) fills
  `Name` for the canonical units (dag → Dekagramm, Pkg → Packung, ml → Milliliter, …).
- **Tests:** the parser table, and a rejected unit returns the 400 shape.

## I7 — Recommendation rank favours items you just bought — DONE
- **Done:** migration `RecommendationOverdueRank` (median interval, `Weight` = ratio, `Rank` = -priority
  with decay beyond 2x and a Count/(Count+5) confidence factor; `orderby r.Rank` unchanged). Dry run on
  a prod copy: integrity ok, top 20 for list 1 is now staples at 1.5-2.5x (Gurke, Zwiebel, Klopapier...).
- **Evidence:** Äpfel ranks #4 one week after purchase on a 13.7-day cycle, because `Rank =
  |avgDiff − since| / count`. The absolute value treats "early" the same as "overdue", and dividing
  by count lets frequent items dominate.
- **Fix:** a new migration that recreates the `Recommendations` view (the pattern is in
  `Migrations/20231207150407_Recommendations.cs`; keep `recommend.sql` in sync) using the overdue
  ratio `since / avgDiff`, ordered by that ratio descending. Keep `count > 2`. Use the median
  interval instead of the mean if SQLite makes it cheap; otherwise clamp single huge gaps (for
  example, ignore intervals more than 4× the item's typical interval). Keep the `Rank` column name
  so the `RecommendationViewModel` is unchanged, but flip the sort direction in
  `ShoppingListController.GetRecommendations` (`orderby r.Rank`) or store `-ratio`.
- **Tests:** seeded histories (due, early, overdue) come out in the expected order. This uses the same
  SQLite-in-temp-file setup as the existing tests.

## I8 — Housekeeping (no code)
- The never-approved account: promote it or delete it via `Account/Promote` or the user list.
- Empty lists 7 (Gewand Kinder) and 8 (Schule): keep them or delete them.
- After I5 ships: run the merge tool once over the suggested groups. Then run declutter over the
  362 single-purchase and 318 year-stale items.
- Observation only: names typed without spaces (`TkPommes`, `JauseLiri`) are a typing habit. I4's
  match key makes them harmless; nothing to enforce.

## I9 — Orphaned ItemHistory rows — DONE

- **Evidence:** `PRAGMA foreign_key_check` on the prod snapshot reports 109 `ItemHistories` rows whose
  `ShoppingListItemId` no longer exists — deletes that ran without foreign-key enforcement, so the
  cascade never fired. Unreachable for every consumer, since history is always read through its item.
- **Done:** data-only migration `DeleteOrphanedItemHistory` deletes them. Dry run on a prod copy with
  all migrations applied: history 5,986 → 5,877, `foreign_key_check` clean. `Down()` is a no-op.

## Order and grouping
1. Deploy I0 alone.
2. I1 + I2 + I3 together: correctness fixes, each with a deterministic cleanup migration.
3. I6: input parsing and the unit cleanup migration.
4. I4 + I5: shared matcher service, add-time suggestions, merge API and UI.
5. I7: the view change.
6. I8: manual follow-up. Then update `FINDINGS.md` (remove F4 once I1 ships) and the `CLAUDE.md`
   sections that change (archive service, push pruning, match key, merge endpoint).

**Verification, applies to every step:** `dotnet test`. Then, before deploying, run the new
migrations against a fresh prod copy: `dotnet ef migrations script <prev> -o x.sql` plus `sqlite3`
on a copy (the method used for I0), and re-run the analysis queries to confirm duplicates are 0,
untrimmed names are 0, `.invalid` subscriptions are 0, and the junk units are gone. Then
`./runLocal.sh` for a smoke test, and `npm run build` plus `npm start` for the client changes (CLAUDE.md
warns the build and the dev server fail independently).

