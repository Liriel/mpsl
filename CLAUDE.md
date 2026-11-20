# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MPS (Multiplayer Shopping List) is a real-time collaborative shopping list application with:
- **Backend**: ASP.NET Core 10.0 with Entity Framework Core, SQLite, and SignalR
- **Frontend**: Angular 21 with Angular Material UI
- **Authentication**: ASP.NET Core Identity with Microsoft Account integration
- **Deployment**: Multi-architecture Docker containers

## Essential Commands

### Backend Development
```bash
# Build and run
dotnet build
dotnet run

# Testing and database
dotnet test
dotnet ef migrations add <MigrationName>
dotnet ef database update
```

### Frontend Development
```bash
cd ClientApp

# Development workflow
npm install
npm start              # Starts dev server on https://localhost:44443
npm run build          # Development build
npm run publish        # Production build (also what the Docker node stage runs)
```
There is no working `npm test` — see Testing below.

### Deployment
```bash
./runLocal.sh          # Builds Dockerfile.x64 and runs it on https://localhost:5001
./publish.sh           # Builds and deploys multi-arch Docker images
```
`publish.sh` no longer builds the client first — each image builds its own SPA (see Deployment
Architecture), so a stale or missing local `ClientApp/dist` cannot end up in a shipped image.

`runLocal.sh` is the integration-test path: same image as production, foreground with logs, Ctrl-C to
stop. `-p <port>` remaps the host port, `--no-build` reuses the existing `mpsl:local` image, `--fresh`
drops the data/key volumes so migrations re-seed from scratch.

- Serves **`https://localhost:5001`** — the redirect URI registered for the Microsoft app, and the
  same origin `launchSettings.json` uses for a plain `dotnet run`, so sign-in works unchanged.
  Changing the port breaks sign-in.
- Kestrel gets the ASP.NET Core **development certificate as PEM** (`Certificates:Default:Path` +
  `KeyPath`, no pfx password), exported to the gitignored `cert/local-dev.pem`/`.key` and re-exported
  automatically once expired. `~/.aspnet/https` is deliberately left alone — `npm start` has its own
  copy there. `cert/server.pfx` is an unrelated, long-expired certificate; ignore it. If the browser
  complains, `dotnet dev-certs https --trust`.
- Because the container is HTTPS-only, the image's `HEALTHCHECK` (plain HTTP on 8080) cannot apply;
  the script overrides it with `--health-cmd`. Note Docker resets the other health options when
  `--health-cmd` is given, hence the repeated `--health-start-period`.
- Reads the Microsoft OAuth credentials from user secrets (or `Authentication__Microsoft__*` in the
  environment) and refuses to start without them — the auth middleware validates those options on
  *every* request, so a container without them answers 500 everywhere, `/health` included.
- SQLite and the DataProtection keys live in named volumes (`mpsl-local-data`, `mpsl-local-keys`), so
  the local `data/mps.db` is never touched and a login survives a restart.

### Frontend Build System
The client uses the esbuild/Vite-based **`@angular/build:application`** builder.

- `outputPath` is `{ "base": "dist", "browser": "" }` **on purpose**. The default would emit to
  `dist/browser/`, but all three `Dockerfile.*` do `COPY --from=client /client/dist/ ./wwwroot/` and
  `mps.csproj` globs `$(SpaRoot)dist\**` for local publishes. Leave the empty `browser` segment alone
  unless you update those too.
- **SignalR needs two separate workarounds — both are load-bearing.** SignalR's published `dist/esm`
  build requires `ws`, `eventsource`, `node-fetch`, `fetch-cookie`, `tough-cookie` and
  `abort-controller`, and its `browser` field only remaps the *source* path
  (`./src/DynamicImports.ts`), so bundlers resolving the shipped build pull them in and drag node
  built-ins (`url`, `http`, `https`, `events`, `util`) into a browser bundle. All of those requires
  sit behind a `Platform.isNode` guard in `HttpConnection` and never run in a browser.
  - `build.externalDependencies` lists the six packages. Without it, `ng build` fails outright with
    `Could not resolve "url"`.
  - `serve.prebundle.exclude` lists `@microsoft/signalr`. Without it, `ng serve` **builds fine but
    serves a blank page**: Vite's dev-server dependency pre-bundler turns each externalised package
    into a static `import * as m from "tough-cookie"` facade, which the browser cannot resolve
    (`Failed to resolve module specifier`). Excluding SignalR from pre-bundling makes the dev server
    bundle it through esbuild the same way the production build does, so the requires stay inside
    the guarded branch.

  Note the failure modes are asymmetric — a production build passing says nothing about whether
  `ng serve` still boots. Check both after touching either option.
- The production build warns that the initial bundle (~1.36 MB) exceeds the 1 MB budget. The error
  threshold is 1.5 MB, so it is a warning only.

## Architecture & Code Structure

### Backend Architecture

**Controller → Service → Repository → DataContext (SQLite)**

- **`EntityController<TEntity, TEditViewModel, TListViewModel>`**: generic CRUD base providing
  pagination (`skip`/`take`), sorting, AutoMapper projection and validation. Specialized controllers
  add or override endpoints. Override the `PostMapping()` hook for custom post-map logic, and the
  `ApplyAccessFilter()`/`CanAccess()` hooks to enforce access (see Authorization below).
- **Repository Pattern**: `IRepository` and `PersistedRepository`. `SaveChanges()` automatically
  stamps `LastChangeByUserId`/`LastChangedDate` on any entity implementing `ITrackLastChange`.
- **Generic Services**: `IEntityService<T>` (`Validate`/`AddOrUpdate`/`Delete`/`DeleteRange`),
  registered transient. `EntityController.Delete` goes through the service, not `IRepository`
  directly, so the type-specific hooks (notably the SignalR removal broadcast) run.
- **Identity Services**: `IUserIdentityService` for user context, registered scoped. Not reliable
  inside SignalR hub invocations — use `Context.User` there.
- **AutoMapper**: object mapping between models and ViewModels (`MyProfile`). Edit VMs extend
  `EditViewModel` (which carries `Id`).
- **SignalR Hub**: `LiveHub` at `/live` for real-time notifications.
- **Result types**: `EntityOperationResult<T>` in the service layer,
  `WebOperationResult<TEditVM, TEntity>` at the controller layer.
- **One error shape for rejected writes.** A write that is refused answers **400** with the
  `{ success, entity, validationErrors }` body — never 200 with `success: false`, and never a bare
  string. That holds for data-annotation failures too: `[ApiController]` rejects an invalid
  `ModelState` in a filter before the action runs, so `Program.cs` points
  `ApiBehaviorOptions.InvalidModelStateResponseFactory` at `ValidationFailureResult` instead of
  letting it emit RFC 7807 ProblemDetails. On the client, `ServerValidation.Show()` puts each error
  on its `FormControl` (as the `server` error key) and returns whatever was left over for the
  component to display. New write endpoints should follow the same convention.
- **Keep an edit view model's constraints in step with the entity's.** A field that is `[Required]`
  on the model but not on the VM turns a 400 into a 500 at the database
  (`ShoppingListEditViewModel.Name` did exactly that).
- **On update, `null` means "not supplied" and must not overwrite.** `UnitShortName` and `ShortName`
  are both optional on the item edit VM but meaningful on the entity, so `PostMapping` skips a null
  `UnitShortName` and the AutoMapper profile puts a `Condition` on `ShortName`; an *empty* string
  still clears the unit. Beware reading the navigation back afterwards — `model.Unit?.Id` re-triggers
  the lazy-loading proxy and resurrects the unit you just cleared, so keep it in a local.
- **The unit field is not for quantities.** `UnitParser.Split` (`Infrastructure/UnitParser.cs`) runs in
  `ShoppingListController.AddItem` and `ItemController.PostMapping` before `GetOrCreateUnit`: when the
  amount is null/0 and the unit text is `digits[letters]`, `"500g"` becomes amount 500 + unit `g`,
  while `"2x"`, `"6er"` and `"3"` become amount only (unit cleared). A set amount is never overwritten.
  `IUnitService.GetOrCreateUnit` returns an `EntityOperationResult<Unit>` and refuses to *create* a unit
  with no letter in it (`"10"`, `"..."`) with a 400 on `UnitShortName`; existing units are still found.
  `EntityController.PostMapping` returns a `ValidationError` (null = fine) so a hook can reject a write.
  `GET api/Unit/search` only offers units with a long name or used 3+ times (items + history), so the
  autocomplete needs no client-side filtering. Migration `UnitCleanup` did the one-off data cleanup.
- **`NameHelper`**: trims and collapses whitespace, normalizes German umlauts (ä→ae, ö→oe, ü→ue,
  ß→sz) and upper-cases. Search and duplicate detection go through the `NormalizedName` field, not
  `Name`. The AutoMapper maps for item add/edit trim `Name`; a whitespace-only name fails `[Required]`
  with the usual 400.
- **`NameHelper.GetMatchKey` is the fuzzy key, `NormalizedName` stays the exact one.** The match key
  lower-cases, spells out umlauts, folds `ß` and `sz` to `ss` and drops everything that is not a
  letter or digit ("Weißwein"="Weisswein", "Wiener wurst"="Wienerwurst"). It is computed in memory,
  never stored — do not change `NormalizedName` semantics, search and uniqueness depend on them.
- **`IItemMatchService` (scoped) is the single matcher** behind the add-time suggestion, the search
  fallback and the merge tool's duplicate groups, so they always agree. `AreNear(keyA, keyB)`: same
  key, or same key after stripping one plural suffix (`en/er/n/s/e`, key longer than 4), or edit
  distance ≤ 1 (shorter key 5–7 chars) / ≤ 2 (≥ 8); keys differing in their digits never match
  ("Pizza 1"/"Pizza 2"). **No substring matching** (Wein/Portwein). It loads `(Id, Name, history
  count, last purchase)` of one list (~800 rows) and compares in memory. Known residual false
  positives on prod data are distance-2 pairs like Duschgel/Waschgel — acceptable because the merge
  UI lets a person pick. `FindDuplicateGroups` clusters transitively (union-find) but a component
  over `MaxGroupSize` (6) falls back to star clustering around the best-established item. The view
  models are mapped by hand in the controller: AutoMapper 11 throws on a positional `record` source
  under .NET 10 and would break *every* mapping at startup.
- **Endpoints on `ShoppingListController`** (all `ApprovedUser` + `CanAccessList`):
  `GET {id}/suggest?name=` (≤3 near items by purchase count; empty when the name already matches
  exactly), `GET {id}/duplicates` (array of groups, each an array of `ItemMatchViewModel`),
  `POST {id}/merge` body `{ targetId, sourceIds[] }` → the surviving item, or a standard 400 when an
  id is outside the list, the target is among the sources or no source is given. `SearchItems` also
  matches on the match key.
- **`IItemMergeService.Merge`** repoints source `ItemHistories` to the target, *removing* rows whose
  `CheckDate` the target already has (unique index), picks the most active status (Open > Checked >
  Archived, later `CheckDate` breaks ties; the winner's amount/unit/check date come along), keeps the
  target's name/unit and takes a source hint only if the target has none, then
  `ChangeTracker.DetectChanges()` and `IEntityService.DeleteRange(sources)` — the DetectChanges is
  load-bearing, otherwise EF's cascade deletes the just-moved history with the old parent. One save;
  the target change is broadcast directly (not via `AddOrUpdate`, which would fire a web push).
- **`RemoveItem` deletes instead of archiving** when the item has no `ItemHistory` and was added
  under 10 minutes ago (typo cleanup), via `IEntityService.Delete` so the removal broadcast runs.
- **Archiving lives in `IItemArchiveService.ArchiveStale`** (scoped; `GetItems` calls it). Each stale
  checked item is *claimed* with a conditional `ExecuteUpdate … where Id==x && Status==Checked &&
  CheckDate==seen` inside a transaction, and the `ItemHistory` row is written only when that affected
  exactly one row, so concurrent `GET`s cannot record a purchase twice. `ItemHistories` also has a
  unique index on `(ShoppingListItemId, CheckDate)` as a backstop. It goes through `DataContext`
  directly because `IRepository` has no way to add an `ItemHistory`.
- **Item kinds (product vs task).** `ShoppingListItem.Kind` (`Model/ItemKind.cs`: `Product = 0`,
  `Task = 1`, serialized as a string) separates things to buy from errands and "go to that shop"
  reminders ("Tanken", "Apotheke"). A new item's kind is: explicit `Kind` on the add VM, else a
  leading `!` in the typed name (prefix stripped, whitespace trimmed: `!Tanken` is the task "Tanken"),
  else `IItemKindClassifier` (singleton, pure, `Services/ItemKindClassifier.cs`). The classifier works
  on `NameHelper.GetMatchKey` and says Task when the key contains `packen`, `tanken`, `abholen`,
  `bringen`, `anrufen`, `zahlen`, `termin`, `erledigen` or `schularbeit`, ends with `zeiten` or
  `plaene`, or equals the match key of *another* list's name (so "Apotheke" on the shopping list is a
  reminder to go there). There is deliberately no generic "-en" rule (Bananen, Nudeln, Kartoffeln stay
  products); the cases are pinned in `Tests/ItemKindTests.cs` and the rules were run over all prod
  names without a false positive. The kind is **learned per item**: a re-added item keeps its stored
  kind, so a user correction (item dialog "Todo" toggle, or the snackbar "Undo" shown after an
  auto-classified add) sticks; only an explicit `Kind` or `!` changes it. `AddItem` reports
  `kind`, `kindAuto` and `kindReason` on the returned entity. On `ShoppingListItemEditViewModel`
  `Kind` is nullable and `null` means "not supplied" (AutoMapper `Condition`), like `ShortName`.
  - **Tasks write no history.** `ItemArchiveService` archives a checked task through the same atomic
    claim but adds no `ItemHistory` row; the `AddItemKind` migration also deleted the existing history
    of the tasks it tagged (by name, never by id). Changing a kind later does not touch history.
  - **Exclusions:** the `Recommendations` view (`Kind = 0`), `ItemMatchService` duplicate groups and
    near-match suggestions (an *exact* match against a task still silences the suggestion). Search /
    autocomplete and the declutter candidates keep tasks; declutter exposes `Kind` and the UI shows a
    "Todo" marker. Web push is unchanged. The migration's SQL backfill approximates the classifier
    (same words, approximate match key) plus an explicit prod name list.
  - Client: tasks show a `task_alt` icon in the `.avatar` instead of the short name.
- **`AppDbInitializer`**: runs migrations and seeds the `user`/`admin` roles on startup.

### Key Directories
```
Controllers/           # Web API controllers
Services/             # Business logic services  
Model/               # Entity models and DataContext
Infrastructure/      # Shared utilities and constants
ViewModels/         # Data transfer objects
Hubs/               # SignalR hubs for real-time updates
ClientApp/          # Angular frontend application
Tests/              # XUnit tests (minimal coverage currently)
```

### Database Models
Core entities: `ShoppingList`, `ShoppingListItem`, `Unit`, `Recommendation`, `ItemHistory`
- All entities track modification user/timestamp
- SQLite database with Entity Framework Code First
- Recommendation algorithm in `recommend.sql`
- **`Recommendations` view ranking** (`recommend.sql`, mirrored in `RecommendationOverdueRank`):
  `Weight` = overdue ratio `Since / max(median purchase interval, 1 day)`; `Rank` = `-priority`, so the
  ascending `orderby r.Rank` lists the most promising item first. Priority rises with the ratio up to 2,
  then decays as `2/(ratio-1)` (an item at 10x is probably no longer bought), scaled by
  `Count/(Count+5)` so staples beat rarities. `AvgDiff` stays the mean, for display only. The view is
  raw SQL, so changing it needs a migration plus the `recommend.sql` copy (`Tests/RecommendationRankTests.cs`).
  It also filters `Kind = 0`, so tasks (see Item kinds) never appear (`AddItemKind` migration).
- Items are unique per shopping list (composite unique index on Name + ShoppingListId)
- ItemHistory records purchase events for analytics and decluttering
- **`HasData` seeds must be deterministic.** Since EF Core 9, `Database.Migrate()` throws
  `PendingModelChangesWarning` when the model differs from the migration snapshot, and a seed built
  from `new ApplicationUser { … }` differs on every build because `IdentityUser`'s constructor
  generates a fresh `SecurityStamp`/`ConcurrencyStamp`. `DataContext.OnModelCreating` therefore pins
  both stamps of the SYSTEM user to the literals already baked into the snapshot. Never reintroduce a
  `Guid.NewGuid()`/`DateTime.Now` into a seed — it takes the whole app down at startup, not just
  migrations.
- **All persisted `DateTime`s are UTC.** `DataContext.ConfigureConventions` registers a `DateTime`
  value converter (`Model/UtcDateTimeConverter.cs`) so every value comes back `Kind = Utc` on read and
  System.Text.Json emits the trailing `Z` the Angular client needs. Never use `DateTime.Now` in domain
  code — the `Recommendations` view depends on this, since `JulianDay('now')` is evaluated in UTC. The
  one-off Europe/Berlin → UTC backfill lives in the `UtcTimestamps` migration and deliberately skips
  `PushSubscriptions.CreatedAt` (already UTC) and rows before 1990 (the `0001-01-01` sentinel would
  shift to year `0000` and become unparsable).

### Frontend Architecture

**Components → Services (InjectionToken) → `WebRepo` (HTTP) → Backend API**

- Traditional **NgModule** architecture (not standalone components); everything is declared in
  `AppModule`. Components carry `standalone: false`. Because of this, `provideZoneChangeDetection()`
  is passed as `applicationProviders` to `bootstrapModule()` in `main.ts` — Angular 21 no longer
  installs a change-detection scheduler for zone-based apps by default, and removing that line
  silently stops the UI updating on SignalR pushes and other async work.
- Templates use **built-in control flow** (`@if`/`@for`/`@else`), not `*ngIf`/`*ngFor`. Every `@for`
  needs a `track`; prefer the model's `id` (all models implement `IEntity`) over `$index`.
  `*matHeaderCellDef`/`*matCellDef`/`*matRowDef` are CDK table directives, not control flow — they
  stay as-is.
- **InjectionToken-based DI**: each service exports an `IXxxToken` constant and components inject via
  `@Inject(IRepoToken)` rather than the concrete class. Follow this pattern for new services.
- **`WebRepo`** implements `IRepo`. Every request sets `withCredentials: true` for cookie auth;
  `SaveEntity()` dispatches to PUT when `id > 0` and POST when `id === 0`. The generic
  `Get`/`Post`/`Put`/`Delete` take **raw multi-segment path fragments**, so they cannot encode their
  arguments — anything containing user-typed text needs a typed method that builds `HttpParams`
  instead (`SearchItems`, `SearchUnits`, `GetDeclutterCandidates`). That is also why the item and
  unit search endpoints take `[FromQuery] string pattern` rather than a route segment: Kestrel decodes
  `%2F` before routing, so an item name containing `/` would 404 against a `{pattern?}` route even
  with `encodeURIComponent` on the client.
- **Unsubscribe with `destroy$` + `takeUntil`**, declared as a field and completed in `ngOnDestroy`
  (`shopping-list.component.ts`, `login`, `home`, `notification-banner`). The codebase deliberately
  uses one idiom — do not mix in `takeUntilDestroyed`. Subscriptions to a plain HTTP call complete on
  their own and need no teardown; subscriptions to a service-held subject (`AuthService.UserInfo`,
  `PushNotificationService.permission$`) do.
- **Errors have two owners and they must not overlap.** `HttpErrorInterceptor`
  (`infrastructure/HttpErrorInterceptor.ts`, registered via `HTTP_INTERCEPTORS` — the existing
  `provideHttpClient(withInterceptorsFromDi())` is what makes the class form work) handles **401/403**
  by refreshing `IAuthService` and routing to `/login`, and **status 0 or ≥ 500** with a rate-limited
  snackbar. It passes **400 straight through** because `ServerValidation` owns that shape, and it
  always rethrows so component `error` callbacks still run. Components therefore add an `error`
  callback only to unstick their own state (a stuck `isLoading` behind `[app-loading]`), never a
  second snackbar.
  - The interceptor is instantiated on the **first HTTP request**, which is `AuthService`'s
    constructor. Injecting `IAuthServiceToken` into it directly is an NG0200 cycle at bootstrap — it
    must be resolved through `Injector` inside the handler. `Router` and `MatSnackBar` are safe.
- **`AuthService.UserInfo` is a cached `ReplaySubject(1)`, not the raw HTTP call**, so `GET /Account`
  happens once instead of once per guard evaluation and component. `Refresh()` re-fetches and emits
  once, then completes, so callers can sequence on it — `LoginComponent.ClaimAdmin` must, or the guard
  reads the pre-claim roles and bounces back to `/login`. `Refresh()` never errors (it fails closed to
  an anonymous user): the router wraps guards in `first()`, so an error there becomes a
  `NavigationError` and leaves a blank page. It also normalizes `roles` to `[]` — `AccountController`
  omits the field entirely for an anonymous caller, so it arrives as `null`. Watch for truthiness
  checks on `roles` after that: `[]` is truthy.
- **`SignalRNotificationService`** connects to `/live` with auto-reconnect and exposes
  `OnItemChanged()`/`OnItemRemoved()` as observables filtered by shopping list id. Callers must
  `JoinList()` to receive anything (see Real-time Features).
- **`AuthGuard`** supports role-based routes via `data: { "role": "admin" }`, defaulting to `user`.
  This is UX only — the server enforces access independently.
- **Models** extend `ModelBase<T>` (constructor takes `Partial<T>`) and implement `IEntity`
  (`id: number`). Infrastructure types: `DataResult<T>` for pagination, `EntityOperationResult<T>`
  for validation.
- **Responsive**: use the `IAppUiService.IsMobile` observable rather than ad-hoc media queries. In a
  template, compare it explicitly — `@if ((isMobile | async) === true)` / `=== false`, never
  `@if (!(isMobile | async))`. `BreakpointAppUiService` debounces 200 ms, so the async pipe yields
  `null` until the first emission and a negated check would render the desktop branch on a phone for
  that window.
- PWA capabilities with service worker.

## Features

### De-clutter Feature
The declutter feature helps users identify and remove stale or rarely-used items:

**Business Logic:**
- **Minimum Age**: Items must be at least 30 days old to be considered for decluttering (prevents newly added items from appearing)
- **Rarely Purchased**: Items purchased `maxPurchaseCount` times or fewer (default: ≤2)
- **Stale Items**: Items not purchased in `minMonthsSinceLastPurchase` months (default: ≥6 months)
- **Never Purchased**: Items with no history records (but must be 30+ days old)
- Items matching ANY criteria (after age check) are shown as candidates

**API Endpoints:**
- `GET /api/ShoppingList/{id}/declutter?minMonthsSinceLastPurchase=6&maxPurchaseCount=2`
  - Returns declutter candidates with purchase statistics
- `POST /api/ShoppingList/{id}/declutter` (body: `int[]` itemIds)
  - Bulk deletes specified items
  - Returns `{ deletedCount: number }`

**Frontend:**
- Access via menu (⋮) in shopping list toolbar → "Declutter List"
- Material table with selectable rows
- Configurable filters for thresholds
- Shows: item name, avatar, purchase count, last purchase date, days since purchase
- Bulk selection and deletion with confirmation

**Implementation:**
- Backend: `DeclutterService` in `Services/` directory
- Frontend: `DeclutterComponent` in `ClientApp/src/app/declutter/`
- Candidates come from a **single grouped LINQ query**. Do not reintroduce an `.AsEnumerable()` before
  the aggregation — under lazy-loading proxies, touching `item.History` per row costs a query each.
- **Deleting an item deletes its `ItemHistory` too** (the FK cascades, see
  `Migrations/20220914124713_InitialMigration.cs`). This is intentional: every consumer of
  `ItemHistory` — `recommend.sql` and `DeclutterService` alike — reaches it through the item, so
  orphaned history rows would be unreadable rather than useful. `Archived` is the state for "not
  currently on the list"; a delete is a real delete.
- **Merge duplicates** is a collapsed `mat-expansion-panel` at the top of `DeclutterComponent`; the
  groups load on first expand (pairwise matching is not free). Per group the user picks the survivor
  with a radio (default: highest purchase count), can untick individual sources, and the group
  disappears after a successful merge. It needs `MatRadioModule` (added to `AppModule`).
- The add form (`ShoppingListComponent.save`) calls `SuggestItems` first; near matches open
  `ItemSuggestionDialogComponent` ("Did you mean …?": pick an existing name, keep the typed one, or
  dismiss to cancel). No near match or a failed lookup adds immediately — still one tap.
- Bulk delete goes through `IEntityService<T>.DeleteRange`, which saves once for the whole batch and
  then broadcasts one removal per item.

## Development Setup

### Configuration
- **Development URL**: https://localhost:44443 (Angular) with proxy to ASP.NET Core API
- **Same-origin dev**: `environment.ts` sets `ServerUrl: ""`, so all API and SignalR traffic is
  relative and forwarded by `ClientApp/proxy.conf.js`. Its context list must cover every backend
  path — currently `/api`, `/live`, `/Account` and `/signin-microsoft`. A backend path missing from
  that list falls through to the SPA and surfaces as an Angular "cannot match any routes" error.
- **Database**: SQLite at `data/mps.db` (gitignored)
- **Authentication**: Requires Microsoft Account OAuth ClientId/ClientSecret in user secrets
- **CORS**: Configured for localhost:44443 and 192.168.0.96:44443. Not exercised by the dev proxy
  flow; kept as a fallback for running the client cross-origin.

### Authentication Flow
Uses ASP.NET Core Identity with cookie-based authentication. Anyone can sign in with a Microsoft
account, which creates an account with **no roles**; an admin grants the `user` role via
`Account/Promote` before that account can reach any data. The cookie handler is configured to answer
with status codes rather than redirects — 401 when unauthenticated, 403 when authenticated but not
approved — so the SPA always receives a status it can act on.

### Authorization & Access Control
The access model is **admin-approved household sharing**: every approved account can see and modify
every list. Enforcement is on the server; the Angular `AuthGuard` is a UX convenience only.

- Data controllers carry `[Authorize(Policy = "ApprovedUser")]` (role `user` or `admin`), registered
  in `Program.cs`. `AccountController` is deliberately exempt: sign-in must work for an account with
  no roles, and `Account/Claim` must work for the first user before any role exists.
- **Do not add a global authorization `FallbackPolicy`** — it would also cover the static files and
  `MapFallbackToFile`, making the SPA itself unreachable at the login page.
- All access decisions go through **`IShoppingListAccessService`**, which takes an explicit
  `ClaimsPrincipal` (controllers pass `this.User`, the hub passes `Context.User`). It is the single
  seam to change if per-list ownership ever replaces household sharing — call sites should not need
  to change.
- New endpoints keyed by `shoppingListId` or item id must call `CanAccessList`/`CanAccessItem`, and
  new entity controllers should override `ApplyAccessFilter`/`CanAccess`.

### Real-time Features
`INotificationService` broadcasts item changes to **per-list SignalR groups** (`sl{shoppingListId}`),
not to all clients. `LiveHub` requires the same `ApprovedUser` policy and exposes `JoinList`/
`LeaveList`, which verify access before touching a group. Group membership does not survive a
reconnect, so the client re-joins on `onreconnected` and after `Reconnect()` (which runs on every
window focus).

**Web push** is separate from SignalR and deliberately much quieter. `IWebPushNotificationService.
NotifyListActivity()` is a **leading-edge throttle**, not a debounce: the first "something new" on a
list opens a collect window (`PushNotifications:CollectWindowSeconds`, default 10), one notification
goes out summarizing what landed in it, then that `(list, actor)` pair stays silent for
`PushNotifications:CooldownMinutes` (default 5) however much more that user adds. The body's count is
"items in the collect window", not a running total.

- Endpoints whose host is `.invalid` (Chrome's marker for retired FCM subscriptions) are unsubscribed
  *before* sending (`IsDeadEndpoint`); a plain network error never removes a subscription. `Endpoint`
  is unique: `SubscribeAsync` looks up by endpoint alone and updates that row, including `UserId`,
  so a browser now used by another account moves over instead of duplicating.
- The service is a **singleton** and resolves `IPushSubscriptionService` per send from
  `IServiceScopeFactory` — the send happens long after the originating request scope is gone, so it
  must not capture a scoped `DataContext`. Do not re-register it as scoped.
- There is **no `CancellationTokenSource`** in the throttle, and an already-scheduled window must
  never restart its timer. The previous implementation cancelled and reused a token, which made
  adding 2+ items send nothing and permanently poisoned that key.
- Entries are only removed from the throttle dictionary while holding that entry's own lock, and
  `NotifyListActivity` re-checks the `Removed` flag after locking. A freshly created entry is
  indistinguishable from a stale one, so without this another list's call can prune it mid-flight.
- Only *new* items and items coming back onto the list notify — `ShoppingListItemEntityService`
  captures the pre-save status **before** `base.AddOrUpdate`, because afterwards `entity.Id` is
  always non-zero. Plain edits and check/uncheck are silent.
- The audience is approved users only, via `IPushSubscriptionService.
  GetSubscriptionsForApprovedUsersExceptAsync` — the push-side counterpart of
  `IShoppingListAccessService` and the one place to change if per-list membership ever arrives.
- `IWebPushSender` wraps `WebPushClient` purely so tests can record sends instead of hitting the
  network; `TestAppFactory` registers a recording fake and can shorten both windows.

## Development Patterns

### Adding New Entities
1. Create model in `Model/` directory
2. Add DbSet to `DataContext`  
3. Create migration: `dotnet ef migrations add AddNewEntity`
4. Create service implementing `IEntityService<T>`
5. Create controller inheriting from appropriate base
6. Add corresponding Angular service and components

### Testing
- `Tests/ImprovementsTests.cs` covers the archive race and history unique index (I1) and name
  trimming (I2); `Tests/WebPushNotificationTests.cs` also covers `.invalid` pruning and the unique
  endpoint (I3). The `ArchiveRaceNameTrimAndPushEndpoints` migration's data SQL is verified by a dry
  run against a prod copy, not by a unit test.
- Backend: XUnit in `Tests/`, run with `dotnet test` from the repo root. Coverage is limited to
  authorization, SignalR group scoping, web-push throttling, the F6 correctness fixes
  (`Tests/CorrectnessTests.cs` — unit handling, the declutter query's SQL translation and its
  candidate semantics, the 400 error contract, and the query-string search pattern from F8) and the
  F7 UTC switch
  (`Tests/UtcDateTimeTests.cs` — `Kind` round-trip, the trailing `Z` on the wire and the archive
  cutoff; `Tests/UtcBackfillSqlTests.cs` — the DST boundary arithmetic and the generated backfill SQL
  run against in-memory SQLite).
- **Seed dates with `DateTime.UtcNow`, not `DateTime.Now`.** The converter turns a `Local` value into
  the right instant, but a local seed N days back is converted with the offset in force *then*, so a
  seed crossing a DST boundary lands an hour off and any exact day-count assertion fails half the
  year.
- `mps.slnx` is load-bearing: without a solution file `dotnet test` resolves `mps.csproj`, finds no
  test adapter and **exits 0 having run nothing**.
- `Tests/TestAppFactory.cs` swaps in a temp SQLite file and a header-driven auth scheme, so tests
  never touch `data/mps.db`. Because that scheme replaces the cookie handler, cookie-specific
  behaviour needs a direct test (see `Tests/CookieAuthResponseTests.cs`).
- Frontend: none. `angular.json` has no `test` target at all, so `npm test` (`ng test`) fails rather
  than running zero tests. `skipTests: true` in the schematics, so `ng generate` does not create spec
  files either. If frontend tests are ever added, Angular 21's default runner is **Vitest**, not
  Karma.

### Code Formatting
Uses EditorConfig in `ClientApp/.editorconfig`:
- 2-space indentation for most files
- Single quotes for TypeScript
- UTF-8 encoding

TypeScript is `strict: true` but with `strictNullChecks`, `strictPropertyInitialization` and
`noImplicitAny` relaxed to `false` — do not assume null safety is enforced. Angular template
checking is strict (`strictTemplates`), and since v21 `typeCheckHostBindings` is on by default too.
`moduleResolution` is `bundler`. Path aliases `@mps/infrastructure/*` and `@mps` are configured in
`tsconfig.json` but currently unused; existing code uses relative imports.

### Styling
- SCSS with Angular Material **M2** theming. Light/dark/auto themes via `--mps-*` CSS custom
  properties; the choice persists to the `mps-theme-mode` localStorage key.
- The M2 API (`mat.m2-define-palette`, `mat.m2-define-light-theme`, `mat.m2-get-color-from-palette`,
  `mat.$m2-*-palette`) is still exported by Material 21, so `src/theme/_theme.scss` and
  `src/styles.scss` continue to work. Note that since Material 20.1 the M2 components no longer read
  the background/foreground palettes for neutral colours — component neutrals now come from tokens,
  so overriding those palettes will not shift them.
- Shared partials: `shared-page.scss`, `shared-list-item.scss`.
- Component animations use the `flyInOut` and `fadeInOut` triggers; HammerJS provides swipe gestures.

## Deployment Architecture

Multi-architecture Docker deployment supporting AMD64, ARM32v7, ARM64v8, modelled on the sibling
`tempmon.Web` project. `Dockerfile.x64` (Alpine, `runtime-deps`), `Dockerfile.arm32v7` and
`Dockerfile.arm64v8` (Ubuntu 24.04 Noble, `aspnet`). Each is a **three-stage build**:

1. `client` — `node:22-alpine`, `npm ci` then `npm run publish`. **The SPA is built in the image, not
   on the dev machine.** `package.json`/`package-lock.json` are copied before the rest of `ClientApp/`
   so `npm ci` stays cached across source edits. JS output is arch-neutral, so this stage is identical
   in all three files and runs natively on the build host even for the ARM images.
2. `build` — .NET SDK, cross-publishes self-contained with `-r linux-musl-x64` / `linux-arm` /
   `linux-arm64`, so the SDK image is always the host-arch one. `--no-restore` relies on the earlier
   `dotnet restore -r <rid>` layer; if you add a RID, both lines must agree or publish re-restores.
3. final — `COPY --from=build /app ./` then `COPY --from=client /client/dist/ ./wwwroot/`.

Notes:
- **.NET 10 ships no Debian base images at all** — `bookworm-slim` has no 10.x tag and there is no
  released `trixie-slim` either, so the ARM images moved to Noble during the .NET 10 upgrade. The
  published tags are Alpine, Noble, Resolute and Azure Linux; check
  `mcr.microsoft.com/v2/dotnet/aspnet/tags/list` before assuming one exists.
- Source is copied **one `COPY` per directory**. A single `COPY` listing several directories flattens
  their contents into the destination (moby#15858); it happens to compile today because no two source
  files share a basename, but it silently breaks the moment two do.
- Each image has a `HEALTHCHECK` against `/health` (`MapHealthChecks` in `Program.cs`, anonymous and
  registered above the SPA fallback so it returns a status rather than `index.html`). Port defaults to
  8080 — the base images' `ASPNETCORE_HTTP_PORTS` — and is overridable via `HEALTH_PORT`. Alpine
  probes with busybox `wget`; the Noble images ship neither `wget` nor `curl`, so they use bash's
  `/dev/tcp`.
- The ARM files open with `# check=skip=InvalidBaseImagePlatform`: the final stage pins an arm base
  image while building on amd64, which is safe because only `COPY` runs there.
- Private registry at `peach.heisl.org`; automated via `publish.sh`.

## Important Notes

- Frontend and backend run simultaneously during development with hot reloading
- Database migrations run automatically on startup
- All API responses are JSON (no MVC views)
- HTTPS certificates auto-generated via `aspnetcore-https.js`
- Tests cover authorization, hub scoping, web push and the F6 correctness fixes; the rest of the
  codebase has none
- `FINDINGS.md` records known architecture and implementation issues from a code review — check it
  before assuming a rough edge is intentional
- **AutoMapper is pinned to 11.0.1 and its vulnerability warning is an accepted risk.** NU1903 /
  CVE-2026-32933 (a `StackOverflowException` DoS on recursive object graphs) is only fixed in 15.1.1
  and 16.1.1, both released after AutoMapper stopped being MIT-licensed, so there is no upgrade path.
  A `NuGetAuditSuppress` item in `mps.csproj` (and a mirrored one in `Tests/Tests.csproj`, since NuGet
  audits transitive packages by default from .NET 9 on) silences exactly that one advisory and nothing
  else — the build is otherwise warning-free, so keep it that way rather than reaching for `NoWarn`.
- `SQLitePCLRaw.bundle_e_sqlite3` is referenced explicitly only to lift EF Core's pinned 2.1.11 (which
  bundles a SQLite affected by CVE-2025-6965) to 2.1.12. Drop it once EF Core ships a newer pin.
- The app supports multiple themes using angular material themeing mechanics
- Responsive design with a mobile first mindset is encouraged
- do not deviate form the theme / default colors, font sizes and typography unless instructed otherwise
- **The UI language is English.** Item names, hints and list names are user data and often German
  (the matcher and classifier rules are German-aware for that reason), but every label, button,
  dialog, snackbar and validation message the app itself shows must be English.