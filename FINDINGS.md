# Architecture & Implementation Findings

Static review of the MPS codebase (backend + Angular client), 2026-07-27, on branch `notifications`
at commit `39734b6`. Nothing was executed for the original review — all findings came from reading
the source, so line numbers refer to that snapshot.

**Status:** F1 and F3 are **fixed** (2026-07-27, uncommitted in the working tree) — see
[Resolved](#resolved) at the bottom. Line numbers for those two no longer match the code.
Everything else stands.

**Suggested order of attack for what remains:** F2 (rotate the leaked VAPID key — a credential
operation, not a code change), then F5 if push notifications are misbehaving in practice.

---

## Critical

### F1 — Authorization is enforced only in the browser — ✅ FIXED

**Any authenticated Microsoft account can read, modify and delete every shopping list in the system.**

- Registration is open: `Controllers/AccountController.cs:687-699` auto-creates an account for any
  successful Microsoft login (and ignores the `CreateAsync` result).
- Every data endpoint is `[Authorize]` with **no role and no ownership filter** —
  `Controllers/ShoppingListController.cs:12`, `Controllers/ItemController.cs:12`,
  `Controllers/EntityContoller.cs:27`. The `user`/`admin` role model exists (`Promote`/`Demote` in
  `AccountController`) but is checked *only* by `ClientApp/src/app/auth.guard.ts:20`. A demoted user
  keeps full API access via curl.
- The ownership plumbing exists but is never read: `ShoppingList.OwnerUserKey` is written at
  `Services/ShoppingListEntityService.cs:20` and used nowhere. Three TODOs mark the hole:
  `Services/ShoppingListEntityService.cs:27`, `Controllers/ShoppingListController.cs:221`,
  `Controllers/ItemController.cs:452`.
- `EntityController.Get/Put/Delete` are IDOR by construction — they resolve entities by id with no
  scoping (`Controllers/EntityContoller.cs:87-165`).

**Fix direction:** add an `[Authorize(Roles="user")]` baseline, then apply an ownership/membership
predicate inside `GetFilteredEntities()` and on every `repo.Find<T>(id)` path. Note there is
currently no list-sharing model at all (no `ShoppingListMember` entity), so deciding the sharing
semantics is a prerequisite — today "multiplayer" is implemented as "everyone sees everything".

### F2 — VAPID private key committed to git

`appsettings.json:11` contains the VAPID **private** key and is tracked in git (committed in
`d7ffa3d`). Anyone with repo access can sign push messages to your subscribers.

**Fix direction:** rotate the keypair, move the private key to user-secrets / environment variables,
keep only the public key in config. The public key is also duplicated into
`ClientApp/src/environments/environment.ts` and `environment.prod.ts` — that part is fine, it is
public by design, but it must be updated in lockstep when the pair is rotated.

### F3 — SignalR hub is unauthenticated and broadcasts globally — ✅ FIXED

`Hubs/LiveHub.cs:6` has `[Authorize]` commented out, and
`Services/SignalRNotificationService.cs:26,34` sends to `Clients.All` with the group-scoped calls
commented out. An anonymous client connecting to `/live` receives the full item payload (names,
amounts, who checked what) for **every list in the system**.

**Fix direction:** re-enable `[Authorize]`, and switch back to `Clients.Group($"sl{id}")` — this also
requires adding join/leave calls in the hub, since the group name is currently computed and then
discarded and no client ever joins a group.

---

## High

### F4 — A GET request mutates the database

`Controllers/ShoppingListController.cs:242-276` (`GetItems`) archives stale checked items and writes
`ItemHistory` rows on a plain `GET`. Two clients polling concurrently — and the client calls
`refresh()` on every window focus (`ClientApp/src/app/shopping-list/shopping-list.component.ts:103`)
— both observe the same rows as unarchived and both append history entries. Duplicate purchase
records then skew recommendations and declutter statistics. There is no transaction and no
concurrency token anywhere in the model.

**Fix direction:** move archiving to a background job or an explicit idempotent command endpoint.

### F5 — Push-notification batching is broken three ways

All in `Services/WebPushNotificationService.cs`:

1. **Lifetime mismatch.** Registered `Scoped` (`Program.cs:57`) but keeps batch state in the instance
   field `pendingBatches` (line 31). Every HTTP request gets a fresh dictionary, so cross-request
   debouncing never happens. Needs to be a singleton, with DB access resolved per-use from
   `IServiceScopeFactory`.
2. **Cancelled token is reused.** On the second item, line 100 cancels
   `existingBatch.CancellationToken` but never replaces the CTS; line 110 then awaits
   `Task.Delay(30000, <already-cancelled token>)`, which throws immediately. Adding 2+ items within
   the window therefore sends **no** notification at all, and the entry is never removed from the
   dictionary — that `shoppingListId-userId` key is poisoned permanently and leaks memory.
3. **Disposed DbContext.** The delayed `Task.Run` closure calls `SendNotificationToAllExceptAsync` →
   `IPushSubscriptionService` → `IRepository` → scoped `DataContext`, 30 s after the request scope was
   disposed. Expect `ObjectDisposedException` even after fixing (2).

Related:

- `Services/ShoppingListItemEntityService.cs:61` guards with `entity.Id != 0` intending "new items
  only", but it runs *after* `base.AddOrUpdate` has saved, so the id is always non-zero — push fires
  on every edit too.
- `WebPushClient` is newed per scope (line 51), creating a fresh `HttpClient` per request.
- `SendNotificationToAllExceptAsync` notifies *every* user in the system about *every* list,
  regardless of any relationship to that list — same blast radius as F3, and it leaks list names.

### F6 — Assorted correctness bugs

| Where | Problem |
|---|---|
| `Controllers/ShoppingListController.cs:288-291` | `SingleOrDefault` on `NormalizedName`, which has **no unique index** (the unique index is on `Name`+`ShoppingListId`, `Model/ShoppingListItem.cs:12`). "Öl" and "Oel" both normalize to `OEL` → `InvalidOperationException` → 500. |
| `Controllers/ItemController.cs:443-446` | `PostMapping` unconditionally sets `model.Unit = GetOrCreateUnit(viewModel.UnitShortName)`, so any edit that omits the unit silently clears it. |
| `Services/UnitService.cs:23-28` | Returns an unsaved, unattached `Unit` with `Id = 0` and no `Name`; the caller then does `item.UnitId = item.Unit?.Id` → 0 (`Controllers/ShoppingListController.cs:308`). Also `ShortName.ToLower()` compared with `SingleOrDefault` throws if the case-insensitive match hits two rows. |
| `Controllers/EntityContoller.cs:154-165` | Generic `Delete` bypasses `IEntityService`, so no broadcast and no service hooks (the TODO admits it). It also hard-deletes items, and `ItemHistories` cascades (`Migrations/20220914124713_InitialMigration.cs:240`) — contradicting the documented promise in `CLAUDE.md` and `DECLUTTER_FEATURE.md` that deletes preserve `ItemHistory` for analytics. |
| `Controllers/EntityContoller.cs:93-118` | Validation failures return **200 OK** with `Success=false`; other endpoints return 400 with a bare string. Clients cannot handle errors uniformly. |
| `Services/DeclutterService.cs:76-83` | `.AsEnumerable()` pulls every item of the list into memory, then touches `x.HistoryRecords` under lazy-loading proxies → one query per item (N+1). Should be a single grouped LINQ query. |
| `Services/DeclutterService.cs:114-127` | Criteria are combined with `&&`; `CLAUDE.md` states "Items matching ANY criteria are shown". Code and docs disagree on the feature's core semantics — decide which is intended. |
| `Services/DeclutterService.cs:139-152` | `itemService.Delete()` calls `SaveChanges()` per item (N round-trips + N broadcasts), then a redundant `SaveChanges()`. No ownership check beyond `shoppingListId`. |
| `Services/PersistedRepository.cs:36-105` | The audit-stamping block is copy-pasted between `SaveChanges` and `SaveChangesAsync`, and each save issues an extra `Users` lookup by name. |
| project-wide | `DateTime.Now` (local) in domain code vs `DateTime.UtcNow` in `PushSubscription`/notifications. SQLite stores these as text; a container timezone change silently corrupts staleness and recommendation math. |

---

## Medium — architecture

- **The repository abstraction doesn't abstract anything.** `IRepository` exposes raw `IQueryable`, so
  EF/SQLite semantics leak into every service and controller, and lazy-loading proxies
  (`Model/DataContext.cs:47`) mean any property touch can fire a query. `Remove<T>` and `Delete<T>`
  are byte-identical duplicates. The stated goal ("we don't want to tie the service to EF",
  `Services/PersistedRepository.cs:33`) is not achieved. Either drop the layer and use `DataContext`
  directly, or make it a real per-aggregate repository.
- **Connection config is hardcoded in `OnConfiguring`** (`Model/DataContext.cs:40-48`) with a
  CWD-relative `data/mps.db` path — no connection string in config, no way to point at another
  database, no `AddDbContext` options. Combined with the `EntityBase` / `ToView` / `JulianDay`
  SQLite specifics, the "TODO: change for mssql" note in `Model/EntityBase.cs:6` is not reachable
  from here.
- ~~**Service locator in controllers**: `HttpContext.RequestServices.GetRequiredService<IDeclutterService>()`
  at `Controllers/ShoppingListController.cs:376` and `:402`.~~ Fixed alongside F1 — `IDeclutterService`
  is now constructor-injected.
- **Fire-and-forget async**: `INotificationService.BroadCast*` returns `Task` and is called unawaited
  in `Controllers/ItemController.cs:475` and `Services/ShoppingListItemEntityService.cs:44,49,79`.
  Exceptions are unobserved and ordering is not guaranteed.
- **Startup**: migrations run automatically on boot via sync-over-async `.Wait()`
  (`AppDbInitializer.cs:34`, `Program.cs:96`). Acceptable for a single-instance home deploy,
  hazardous with the multi-arch container story in `publish.sh`.
- **Proxy/CORS**: `KnownProxies.Clear()` (`Program.cs:79`, self-labelled HACK) lets any client spoof
  `X-Forwarded-Proto`/`For`, defeating `UseHttpsRedirection` and poisoning logs. CORS origins
  including a hardcoded LAN IP live in code, not config (`Program.cs:44-51`).
- **Dead config**: the conventional route `{controller}/{action=FilteredIndex}/{id?}`
  (`Program.cs:101`) never matches — all controllers are attribute-routed.
- **No global exception handler / ProblemDetails**, so unhandled exceptions surface as raw 500s.
  Several actions also return `IEnumerable<T>` straight from `ProjectTo`, which only executes during
  serialization — an exception there happens after the response headers are sent.
- **`recommend.sql` is a second, drifting copy** of the view defined in
  `Migrations/20231207150407_Recommendations.cs:16`, with the `ItemState` enum value hardcoded as the
  magic number `3`.

## Medium — frontend

- **Subscription leaks**: still present in
  `ClientApp/src/app/notification-banner/notification-banner.component.ts:28-37` (two subscriptions,
  no `OnDestroy`), and `ClientApp/src/app/declutter/declutter.component.ts:52` adds a
  `window.resize` listener that is never removed. *(The `ShoppingListComponent` case was fixed with
  F3 — it now implements `OnDestroy` with `takeUntil`.)*
- **Race → runtime error**: `OnItemChanged`/`OnItemRemoved` dereference `this.items`
  (`shopping-list.component.ts:92,119`), which is `undefined` until the first `refresh()` resolves.
  A push arriving during initial load throws.
- **`AuthService.UserInfo` is a cold observable** (`ClientApp/src/app/services/AuthService.ts:12`) —
  every guard evaluation and every component subscription issues a fresh `GET /Account`. Needs
  `shareReplay(1)`.
- **Unencoded path parameters**: search patterns are concatenated straight into URLs
  (`shopping-list.component.ts:139,147`), so an item name containing `/`, `#` or `%` breaks or
  misroutes the request. `encodeURIComponent` is missing throughout `WebRepo`.
- **No error handling on writes**: `save()`, the toggle call and the list loads `subscribe()` with no
  error callback, and there is no HTTP interceptor — an expired session (401) silently does nothing
  instead of routing to `/login`.
- Service worker is force-enabled in dev (`ClientApp/src/app/app.module.ts:106`, the comment
  acknowledges it); no lazy-loaded routes (everything eager in one `AppModule`); `alert()` used for
  error UX in `notification-banner.component.ts:69` despite `MatSnackBar` being available.

---

## Low — hygiene and doc drift

- ~~`CLAUDE.md` documents a `Tests/` XUnit project and `dotnet test`; **no test project exists**.~~
  The `Tests/` project now exists (created with the F1/F3 fix, 35 tests) along with `mps.slnx`.
  Coverage is limited to authorization and hub scoping — the rest of the codebase still has none,
  and Angular tests remain disabled in `angular.json`.
- `ViewModels/ShoppingListItemViewModel.cs:7` — `shoppingListId` breaks PascalCase among its siblings.
- `Controllers/EntityContoller.cs` — filename typo. `AccountController` sits in namespace
  `tempmon.Web.Controllers`, left over from another project.
- `Infrastructure/Constants.cs` — `SYSUSER_ID` is the all-zeros GUID with a `// TODO: generate new`.
- `Model/PushSubscription.cs` — no unique index on `(UserId, Endpoint)` despite `SubscribeAsync`
  treating that pair as a key; lookups by `Endpoint` are unindexed.
- `Controllers/EntityContoller.cs:54-63` — `PageAndProjectResult` does not guard negative
  `skip`/`take` → `ArgumentOutOfRangeException` → 500.
- Stray artifacts in the repo root: `.swp`, three feature markdown files, `_bmad-output/`.

---

## Resolved

### F1 + F3 — fixed 2026-07-27 (uncommitted, branch `notifications`)

Access model implemented: **admin-approved household sharing** — any account in the `user` or
`admin` role reaches every list, matching the existing approval UX; unapproved accounts are now
rejected by the server, not just by the `AuthGuard`.

**Authorization (F1)**
- `Program.cs` — `ApprovedUser` policy (`RequireRole("user","admin")`). Deliberately *not* a global
  `FallbackPolicy`: that would 401 the SPA's static files and `MapFallbackToFile` and lock everyone
  out at the login page. `AccountController` is unchanged, so `Index`/`SignIn`/`SignInCallback`
  stay anonymous and `Claim` stays a bare `[Authorize]` — a first user with no roles must still be
  able to claim admin.
- `Services/ShoppingListAccessService.cs` (new) — the single seam for access decisions. Takes
  `ClaimsPrincipal` explicitly (controllers pass `this.User`, the hub passes `Context.User`) because
  `IHttpContextAccessor` is unreliable inside SignalR invocations. **Swap this one class to move to
  per-list ownership/membership; no call site should need to change.**
- `Controllers/EntityContoller.cs` — virtual `ApplyAccessFilter`/`CanAccess` hooks enforced in
  `Index`, `Get`, `Put` and `Delete`; overridden in `ShoppingListController` and `ItemController`.
  Note `Get` now returns `IActionResult` (404 for missing, 403 for inaccessible) instead of the
  view model directly.
- `ShoppingListController` — access checks on all eight list-keyed actions, including `SearchItems`,
  which previously leaked item names from any list. `ItemController.ToggleStatus` likewise.
- Dead `GetFilteredEntities()` overrides removed from the two entity services — they carried the
  "TODO: add filter" comments but had no caller, so enforcement now lives in exactly one place.

**Realtime (F3)**
- `Hubs/LiveHub.cs` — `[Authorize(Policy = "ApprovedUser")]` plus `JoinList`/`LeaveList`, each
  verifying access before touching the group and throwing `HubException` otherwise.
- `Services/SignalRNotificationService.cs` — broadcasts to `Clients.Group($"sl{id}")`, not
  `Clients.All`.
- Client — `JoinList`/`LeaveList` on `INotificationService`; the service awaits the connection
  start, tracks joined ids and **re-joins on `onreconnected` and after `Reconnect()`**, because
  group membership does not survive a reconnect and `Reconnect()` runs on every window focus.

**One extra bug found and fixed during verification.** Every new check calls `Forbid()`, and the
Identity cookie only overrode `OnRedirectToLogin` (→401) — so a forbid produced a **302 to the empty
`AccessDeniedPath`**, handing the SPA `index.html` instead of a 403. Access was still denied, but
the client could not tell. `Program.cs` now has an `OnRedirectToAccessDenied` handler returning 403.
The endpoint tests could not catch this (they run on a header-driven test scheme whose forbid writes
a plain 403), so `Tests/CookieAuthResponseTests.cs` asserts the production cookie configuration
directly — it was confirmed to fail with 302 before the fix.

**Tests** — `Tests/` (new xUnit project, plus `mps.slnx`): 35 tests, all passing. The solution file
is load-bearing: without it `dotnet test` at the repo root resolves `mps.csproj`, finds no test
adapter and **exits 0 having run nothing**. It uses the `.slnx` format (SDK 9.0.200+); the Docker
builds never copy it, so the runtime `sdk:8.0-alpine` image is unaffected.
`AuthorizationTests` covers anonymous→401 / no-role→403 / approved→success across every controller
and custom action, and confirms `/Account` and `/Account/Claim` still work for a role-less user.
`LiveHubTests` covers anonymous and no-role rejection, `JoinList` on a nonexistent list, and group
isolation (an add to list 1 reaches only the connection joined to list 1). `DataContext` gained an
`IsConfigured` guard and an options constructor so tests use a temp SQLite file instead of
`data/mps.db`.

**Verified:** `dotnet build` clean (only the pre-existing `NU1903` AutoMapper advisory),
`dotnet test` 35/35, `npx tsc --noEmit -p tsconfig.app.json` clean. Not manually run against the
real database or a browser — the end-to-end checks in the plan
(`~/.claude/plans/purrfect-strolling-eagle.md`) are still worth doing before deploying, in
particular the two-browser group-isolation and reconnect checks.

**Known follow-up:** `POST api/Item` (generic create) can never succeed — AutoMapper ignores
`ShoppingListId` (`Infrastructure/AutomapperConfiguration.cs`), so `Validate()` always rejects with
"Invalid shopping list". Pre-existing and unrelated to F1/F3; real item creation goes through
`ShoppingListController.AddItem`, which sets the id explicitly. The corresponding test asserts only
the authorization boundary and documents this inline.
