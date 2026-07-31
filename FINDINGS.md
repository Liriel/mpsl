# Architecture & Implementation Findings

Static review of the MPS codebase (backend + Angular client), 2026-07-27, on branch `notifications`
at commit `39734b6`. Nothing was executed for the original review — all findings came from reading
the source, so line numbers refer to that snapshot.

**Status:** the original F1, F3, F5 and F6 were fixed (F1/F3 on 2026-07-27, F5 and F6 on 2026-07-28),
and F7 — the one row of F6 that was deferred rather than fixed at the time — has now been fixed too
(2026-07-31); see commits `bd92e7c`, `09f32de` and the corresponding sections of `CLAUDE.md` for what
the fixes established. Surviving findings keep their original identifiers rather than being
renumbered, so the list is now F2 and F4. Everything listed below still stands.

**Suggested order of attack:** F2 (rotate the leaked VAPID key — a credential operation, not a code
change; push notifications now work, so they are signed with a key that is public in git history),
then F4.

---

## Critical

### F2 — VAPID private key committed to git

`appsettings.json:11` contains the VAPID **private** key and is tracked in git (committed in
`d7ffa3d`). Anyone with repo access can sign push messages to your subscribers.

**Fix direction:** rotate the keypair, move the private key to user-secrets / environment variables,
keep only the public key in config. The public key is also duplicated into
`ClientApp/src/environments/environment.ts` and `environment.prod.ts` — that part is fine, it is
public by design, but it must be updated in lockstep when the pair is rotated.

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

- **Subscription leaks**: `ClientApp/src/app/notification-banner/notification-banner.component.ts:28-37`
  (two subscriptions, no `OnDestroy`), and `ClientApp/src/app/declutter/declutter.component.ts:52`
  adds a `window.resize` listener that is never removed.
- **Race → runtime error**: `OnItemChanged`/`OnItemRemoved` dereference `this.items`
  (`shopping-list.component.ts:92,119`), which is `undefined` until the first `refresh()` resolves.
  A push arriving during initial load throws.
- **`AuthService.UserInfo` is a cold observable** (`ClientApp/src/app/services/AuthService.ts:12`) —
  every guard evaluation and every component subscription issues a fresh `GET /Account`. Needs
  `shareReplay(1)`.
- **Unencoded path parameters**: search patterns are concatenated straight into URLs
  (`shopping-list.component.ts:139,147`), so an item name containing `/`, `#` or `%` breaks or
  misroutes the request. `encodeURIComponent` is missing throughout `WebRepo`.
- **No error handling on writes**: the list loads and the toggle call `subscribe()` with no error
  callback, and there is no HTTP interceptor — an expired session (401) silently does nothing
  instead of routing to `/login`. *(The three write paths that display validation errors — the item
  dialog, the create-list drawer and the add box — were wired up with F6.)*
- Service worker is force-enabled in dev (`ClientApp/src/app/app.module.ts:106`, the comment
  acknowledges it); no lazy-loaded routes (everything eager in one `AppModule`); `alert()` used for
  error UX in `notification-banner.component.ts:69` despite `MatSnackBar` being available.

---

## Low — hygiene and doc drift

- Test coverage is limited to authorization, hub scoping, web push and the F6 correctness fixes — the
  rest of the backend has none, and Angular tests remain disabled in `angular.json`.
- `ViewModels/ShoppingListItemViewModel.cs:7` — `shoppingListId` breaks PascalCase among its siblings.
- `Controllers/EntityContoller.cs` — filename typo. `AccountController` sits in namespace
  `tempmon.Web.Controllers`, left over from another project.
- `Infrastructure/Constants.cs` — `SYSUSER_ID` is the all-zeros GUID with a `// TODO: generate new`.
- `Model/PushSubscription.cs` — no unique index on `(UserId, Endpoint)` despite `SubscribeAsync`
  treating that pair as a key; lookups by `Endpoint` are unindexed.
- `Controllers/EntityContoller.cs:54-63` — `PageAndProjectResult` does not guard negative
  `skip`/`take` → `ArgumentOutOfRangeException` → 500.
- Stray artifacts in the repo root: `.swp`, three feature markdown files, `_bmad-output/`.
- **Known follow-up from F1:** `POST api/Item` (generic create) can never succeed — AutoMapper ignores
  `ShoppingListId` (`Infrastructure/AutomapperConfiguration.cs`), so `Validate()` always rejects with
  "Invalid shopping list". Real item creation goes through `ShoppingListController.AddItem`, which
  sets the id explicitly; the authorization test documents this inline.
