# Running MPS with Docker

MPS ships as a single container: the ASP.NET Core backend serves the API, the SignalR hub and the
pre-built Angular app, and keeps its data in a SQLite file. You need:

- Docker (with BuildKit, the default in current versions)
- a **public HTTPS hostname** for the app, e.g. `https://shop.example.com` — sign-in with Microsoft,
  the service worker and web push all require HTTPS
- a reverse proxy that terminates TLS (Caddy, Traefik, nginx, …)
- a Microsoft app registration for sign-in
- a VAPID key pair for push notifications

## 1. Build the image

There is no public image, so build one from the repository. Pick the Dockerfile for the machine that
will run the container:

| Target                    | Dockerfile          | Base image                 |
|---------------------------|---------------------|----------------------------|
| x86-64                    | `Dockerfile.x64`    | Alpine (`runtime-deps`)    |
| 64-bit ARM (Raspberry Pi 4/5, …) | `Dockerfile.arm64v8` | Ubuntu (`aspnet`)    |
| 32-bit ARM                | `Dockerfile.arm32v7`| Ubuntu (`aspnet`)          |

```bash
git clone https://github.com/Liriel/mpsl.git
cd mpsl
docker build -f Dockerfile.x64 -t mpsl .
```

The build is self-contained: it compiles the Angular client in a Node stage and cross-compiles the
.NET app for the target architecture, so the ARM images can be built on an x86 machine. Nothing needs
to be installed on the build host besides Docker.

## 2. Register the app with Microsoft

Sign-in uses Microsoft accounts (personal Outlook/Live accounts or work/school accounts).

1. Open the [Azure portal → App registrations](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
   and choose **New registration**.
2. **Supported account types:** include *personal Microsoft accounts* (e.g. "Accounts in any
   organizational directory and personal Microsoft accounts").
3. **Redirect URI:** platform *Web*, value `https://<your-host>/signin-microsoft` — it must match
   the public URL exactly, including the scheme.
4. After creating it, note the **Application (client) ID**, then under **Certificates & secrets**
   create a client secret and note its **value**.

The app refuses every request (including `/health`) with a 500 if these credentials are missing, so
the container will never turn healthy without them.

## 3. Generate VAPID keys

Web push needs a key pair that belongs to your installation:

```bash
npx web-push generate-vapid-keys
```

Keep the private key secret. Changing the keys later invalidates every existing push subscription;
users then have to re-enable notifications.

> **The public key is also compiled into the client.** Set `vapidPublicKey` in
> `ClientApp/src/environments/environment.prod.ts` to your public key *before* building the image —
> otherwise browsers subscribe with the wrong key and pushes fail. The rest of the app works without
> push.

## 4. Run the container

```bash
docker run -d --name mpsl --restart unless-stopped \
  -p 127.0.0.1:8080:8080 \
  -e Authentication__Microsoft__ClientId=<client-id> \
  -e Authentication__Microsoft__ClientSecret=<client-secret> \
  -e VapidKeys__PublicKey=<vapid-public-key> \
  -e VapidKeys__PrivateKey=<vapid-private-key> \
  -e VapidKeys__Subject=mailto:<you@example.com> \
  -v mpsl-data:/app/data \
  -v mpsl-keys:/root/.aspnet/DataProtection-Keys \
  mpsl
```

Or with Compose — put the secrets in an `.env` file next to it and keep that file out of version
control:

```yaml
# compose.yaml
services:
  mpsl:
    image: mpsl
    restart: unless-stopped
    ports:
      - "127.0.0.1:8080:8080"
    environment:
      Authentication__Microsoft__ClientId: ${MS_CLIENT_ID}
      Authentication__Microsoft__ClientSecret: ${MS_CLIENT_SECRET}
      VapidKeys__PublicKey: ${VAPID_PUBLIC_KEY}
      VapidKeys__PrivateKey: ${VAPID_PRIVATE_KEY}
      VapidKeys__Subject: mailto:you@example.com
    volumes:
      - mpsl-data:/app/data
      - mpsl-keys:/root/.aspnet/DataProtection-Keys

volumes:
  mpsl-data:
  mpsl-keys:
```

What the pieces are for:

| Setting | Purpose |
|---|---|
| port `8080` | the app's plain-HTTP port inside the container; publish it to localhost only and let the proxy handle TLS |
| `/app/data` | the SQLite database `mps.db`. **Back this up** — it is all of your data |
| `/root/.aspnet/DataProtection-Keys` | keys that sign the login cookies; without a volume, everyone is logged out on every container restart |
| `Authentication__Microsoft__*` | the app registration from step 2 |
| `VapidKeys__*` | the key pair from step 3 (overrides the defaults in `appsettings.json`) |
| `PushNotifications__CollectWindowSeconds`, `PushNotifications__CooldownMinutes` | optional: how push notifications are batched (defaults 10 s and 5 min) |

The database schema is created and migrated automatically on startup, so upgrading is: rebuild the
image, then recreate the container with the same volumes.

## 5. Put a reverse proxy in front

The proxy must forward the original host and send `X-Forwarded-Proto: https`, otherwise the app
believes it is served over HTTP and builds a wrong sign-in redirect. It must also pass WebSocket
upgrades through for the live updates on `/live`.

[Caddy](https://caddyserver.com/) does all of that, and fetches a certificate, with two lines:

```caddyfile
shop.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

For nginx, the relevant parts are:

```nginx
location / {
    proxy_pass         http://127.0.0.1:8080;
    proxy_http_version 1.1;
    proxy_set_header   Host              $host;
    proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_set_header   Upgrade           $http_upgrade;
    proxy_set_header   Connection        "upgrade";
}
```

The app accepts forwarded headers from any address, so do not expose port 8080 publicly — only the
proxy should be able to reach it.

## 6. First sign-in and adding users

1. Open `https://<your-host>` and sign in with Microsoft. The very first account to sign in on an
   empty installation is offered to **claim the admin role** — do that.
2. Everyone else who signs in gets an account without access and is told that the admin has to
   confirm it. As admin, open **Users** from the menu and approve them.

All approved users share all lists — MPS is built for a household, not for strangers.

## Checking on it

```bash
docker inspect --format '{{.State.Health.Status}}' mpsl   # healthy once it serves requests
docker logs -f mpsl
```

The image's health check probes `http://localhost:8080/health`. If you change the app's port (e.g.
with `ASPNETCORE_HTTP_PORTS`), set `HEALTH_PORT` to match.

## Trying it locally

`./runLocal.sh` builds the x86-64 image and runs it on `https://localhost:5001` with the ASP.NET Core
development certificate. It reads the Microsoft credentials from `dotnet user-secrets` (or the
environment), so register `https://localhost:5001/signin-microsoft` as an additional redirect URI to
use it. See the comments at the top of the script for its options.
