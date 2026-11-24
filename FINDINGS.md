# Architecture & Implementation Findings

Static review of the MPS codebase (backend + Angular client), 2026-07-27, on branch `notifications`
at commit `39734b6`. Nothing was executed for the original review — all findings came from reading
the source, so line numbers refer to that snapshot.

**Status:** the original F1, F3, F5 and F6 were fixed (F1/F3 on 2026-07-27, F5 and F6 on 2026-07-28),
F7 — the one row of F6 that was deferred rather than fixed at the time — was fixed on 2026-07-31, and
F8, the *Medium — frontend* block, on the same day, and F4 (the archive race) by improvement I1 on 2026-11-20; see commits `bd92e7c`, `09f32de`, `9ca9436` and
the corresponding sections of `CLAUDE.md` for what the fixes established. Surviving findings keep
their original identifiers rather than being renumbered, so the list is now F2 only. Everything
listed below still stands.

**Suggested order of attack:** F2 (rotate the leaked VAPID key — a credential operation, not a code
change; push notifications now work, so they are signed with a key that is public in git history),
then the remaining items below.

---

## Critical

### F2 — VAPID private key committed to git

**Partly fixed.** `appsettings.json` is no longer tracked (it is gitignored, `appsettings.example.json`
is the template) and was purged from the git history together with the copy of the key in
`Tests/WebPushNotificationTests.cs`. The key had already been pushed to the public GitHub repository
before the purge, though, so it must still be treated as leaked — it remains valid until rotated, and
anyone holding it plus a subscriber endpoint can sign push messages to that subscriber.

**Remaining:** rotate the keypair, move the private key to user-secrets / environment variables,
keep only the public key in config. The public key is also duplicated into
`ClientApp/src/environments/environment.ts` and `environment.prod.ts` — that part is fine, it is
public by design, but it must be updated in lockstep when the pair is rotated.

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

Fixed on 2026-07-31 (F8): the subscription/listener leaks, the `this.items` race, the cold
`AuthService.UserInfo`, the unencoded search patterns, the missing error handling on writes and the
`alert()` for error UX. See the *Frontend Architecture* section of `CLAUDE.md` for what the fixes
established. Two rows were deliberately **not** fixed:

- **Service worker is force-enabled in dev** (`ClientApp/src/app/app.module.ts:111`). Kept on
  purpose — push notifications cannot be exercised under `ng serve` otherwise, and that is an active
  area of work. Revisit once push is stable.
- **No lazy-loaded routes** — every component is declared in one eager `AppModule`. Splitting the
  admin pages (`user-list`, `notifications-test`) and `declutter` into lazy feature modules needs a
  shared Material module first. Deferred: the initial bundle is ~1.36 MB against a 1.5 MB error
  threshold, and this is a single-household app, so bundle size is not a real problem.

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
