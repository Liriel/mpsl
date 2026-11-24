# MPS — Multiplayer Shopping List

A shopping list for a household, shared in real time. Everyone with access sees items appear, get
checked off and disappear as it happens, and gets a push notification when someone adds to a list
while the app is closed.

## Features

- **Live collaboration** — changes reach every open device instantly over SignalR
- **Push notifications** — batched, so a burst of additions becomes one notification; tapping it
  opens the list
- **Installable PWA** — works like an app on phones, with light and dark themes
- **Smart adding** — "Did you mean …?" suggestions for near-duplicates, `500g` split into amount and
  unit, errands ("Tanken", "Apotheke") told apart from products
- **Recommendations** — suggests items that are probably due again, based on purchase history
- **Declutter** — finds stale and rarely bought items and merges duplicates
- **Sign in with Microsoft**, with admin approval for new accounts

## Running it

- **With Docker:** see [docs/docker.md](docs/docker.md) for building the image, configuring sign-in
  and push notifications, and running it behind a reverse proxy.

## Development

Requirements: .NET SDK 11, Node.js 22, and a Microsoft app registration whose redirect URIs include
`https://localhost:5001/signin-microsoft`.

```bash
# backend credentials, once
dotnet user-secrets set Authentication:Microsoft:ClientId <client-id>
dotnet user-secrets set Authentication:Microsoft:ClientSecret <client-secret>

# push notification keys (optional): copy the template and fill in a VAPID key pair
# appsettings.json is gitignored - never commit it
cp appsettings.example.json appsettings.json

# backend on https://localhost:5001
dotnet run

# frontend on https://localhost:44443 (proxies API calls to the backend)
cd ClientApp
npm install
npm start
```

The SQLite database is created at `data/mps.db` and migrated automatically on startup. Run the
backend tests with `dotnet test` from the repository root.

## Tech stack

ASP.NET Core with Entity Framework Core (SQLite), ASP.NET Core Identity and SignalR on the backend;
Angular with Angular Material on the frontend.

## License

[MIT](LICENSE)
