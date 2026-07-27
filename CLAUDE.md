# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MPS (Multiplayer Shopping List) is a real-time collaborative shopping list application with:
- **Backend**: ASP.NET Core 8.0 with Entity Framework Core, SQLite, and SignalR
- **Frontend**: Angular 17 with Angular Material UI
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
npm run publish        # Production build for deployment
npm test
```

### Deployment
```bash
./publish.sh           # Builds and deploys multi-arch Docker images
```

## Architecture & Code Structure

### Backend Architecture

**Controller → Service → Repository → DataContext (SQLite)**

- **`EntityController<TEntity, TEditViewModel, TListViewModel>`**: generic CRUD base providing
  pagination (`skip`/`take`), sorting, AutoMapper projection and validation. Specialized controllers
  add or override endpoints. Override the `PostMapping()` hook for custom post-map logic, and the
  `ApplyAccessFilter()`/`CanAccess()` hooks to enforce access (see Authorization below).
- **Repository Pattern**: `IRepository` and `PersistedRepository`. `SaveChanges()` automatically
  stamps `LastChangeByUserId`/`LastChangedDate` on any entity implementing `ITrackLastChange`.
- **Generic Services**: `IEntityService<T>` (`Validate`/`AddOrUpdate`/`Delete`), registered transient.
- **Identity Services**: `IUserIdentityService` for user context, registered scoped. Not reliable
  inside SignalR hub invocations — use `Context.User` there.
- **AutoMapper**: object mapping between models and ViewModels (`MyProfile`). Edit VMs extend
  `EditViewModel` (which carries `Id`).
- **SignalR Hub**: `LiveHub` at `/live` for real-time notifications.
- **Result types**: `EntityOperationResult<T>` in the service layer,
  `WebOperationResult<TEditVM, TEntity>` at the controller layer.
- **`NameHelper`**: normalizes German umlauts (ä→ae, ö→oe, ü→ue, ß→sz) and upper-cases. Search and
  duplicate detection go through the `NormalizedName` field, not `Name`.
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
- Items are unique per shopping list (composite unique index on Name + ShoppingListId)
- ItemHistory records purchase events for analytics and decluttering

### Frontend Architecture

**Components → Services (InjectionToken) → `WebRepo` (HTTP) → Backend API**

- Traditional **NgModule** architecture (not standalone components); everything is declared in
  `AppModule`. Components carry `standalone: false`.
- **InjectionToken-based DI**: each service exports an `IXxxToken` constant and components inject via
  `@Inject(IRepoToken)` rather than the concrete class. Follow this pattern for new services.
- **`WebRepo`** implements `IRepo`. Every request sets `withCredentials: true` for cookie auth;
  `SaveEntity()` dispatches to PUT when `id > 0` and POST when `id === 0`.
- **`SignalRNotificationService`** connects to `/live` with auto-reconnect and exposes
  `OnItemChanged()`/`OnItemRemoved()` as observables filtered by shopping list id. Callers must
  `JoinList()` to receive anything (see Real-time Features).
- **`AuthGuard`** supports role-based routes via `data: { "role": "admin" }`, defaulting to `user`.
  This is UX only — the server enforces access independently.
- **Models** extend `ModelBase<T>` (constructor takes `Partial<T>`) and implement `IEntity`
  (`id: number`). Infrastructure types: `DataResult<T>` for pagination, `EntityOperationResult<T>`
  for validation.
- **Responsive**: use the `IAppUiService.IsMobile` observable rather than ad-hoc media queries.
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
- Query uses LINQ to join ShoppingListItems with ItemHistory
- Deletes preserve ItemHistory records for future analytics

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

## Development Patterns

### Adding New Entities
1. Create model in `Model/` directory
2. Add DbSet to `DataContext`  
3. Create migration: `dotnet ef migrations add AddNewEntity`
4. Create service implementing `IEntityService<T>`
5. Create controller inheriting from appropriate base
6. Add corresponding Angular service and components

### Testing
- Backend: XUnit in `Tests/`, run with `dotnet test` from the repo root. Coverage is limited to
  authorization and SignalR group scoping.
- `mps.slnx` is load-bearing: without a solution file `dotnet test` resolves `mps.csproj`, finds no
  test adapter and **exits 0 having run nothing**.
- `Tests/TestAppFactory.cs` swaps in a temp SQLite file and a header-driven auth scheme, so tests
  never touch `data/mps.db`. Because that scheme replaces the cookie handler, cookie-specific
  behaviour needs a direct test (see `Tests/CookieAuthResponseTests.cs`).
- Frontend: Angular testing (Jasmine/Karma), currently disabled in angular.json. `skipTests: true`
  in the schematics, so `ng generate` does not create spec files.

### Code Formatting
Uses EditorConfig in `ClientApp/.editorconfig`:
- 2-space indentation for most files
- Single quotes for TypeScript
- UTF-8 encoding

TypeScript is `strict: true` but with `strictNullChecks`, `strictPropertyInitialization` and
`noImplicitAny` relaxed to `false` — do not assume null safety is enforced. Angular template
checking is strict (`strictTemplates`). Path aliases `@mps/infrastructure/*` and `@mps` are
configured in `tsconfig.json` but currently unused; existing code uses relative imports.

### Styling
- SCSS with Angular Material **M2** theming. Light/dark/auto themes via `--mps-*` CSS custom
  properties; the choice persists to the `mps-theme-mode` localStorage key.
- Shared partials: `shared-page.scss`, `shared-list-item.scss`.
- Component animations use the `flyInOut` and `fadeInOut` triggers; HammerJS provides swipe gestures.

## Deployment Architecture

Multi-architecture Docker deployment supporting AMD64, ARM32v7, ARM64v8:
- Alpine Linux base images for minimal footprint
- Self-contained .NET deployments
- Private registry at `peach.heisl.org`
- Automated via `publish.sh` script

## Important Notes

- Frontend and backend run simultaneously during development with hot reloading
- Database migrations run automatically on startup
- All API responses are JSON (no MVC views)
- HTTPS certificates auto-generated via `aspnetcore-https.js`
- Tests cover authorization and hub scoping only; the rest of the codebase has none
- `FINDINGS.md` records known architecture and implementation issues from a code review — check it
  before assuming a rough edge is intentional
- The app supports multiple themes using angular material themeing mechanics
- Responsive design with a mobile first mindset is encouraged
- do not deviate form the theme / default colors, font sizes and typography unless instructed otherwise