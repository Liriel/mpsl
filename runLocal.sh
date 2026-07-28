#!/bin/bash
# Build the amd64 deployment image and run it locally, so the containerised app can be
# exercised end to end: SPA served from wwwroot, API, SignalR and SQLite all as they are
# in production. The Angular client is built inside the image, so nothing from a local
# ClientApp/dist can mask a broken container build.
#
#   ./runLocal.sh              build and run on https://localhost:5001
#   ./runLocal.sh -p 5000      use a different host port
#   ./runLocal.sh --no-build   re-run the image that is already built
#   ./runLocal.sh --fresh      start from an empty database
#
# Serves HTTPS so the Microsoft redirect URI matches the one registered for local
# development. Ctrl-C stops the container; it is removed on exit, the data lives in a
# named volume.
set -euo pipefail

IMAGE=mpsl:local
NAME=mpsl-local
DATA_VOLUME=mpsl-local-data
KEYS_VOLUME=mpsl-local-keys
# https://localhost:5001 is the redirect URI registered for the Microsoft app, so sign-in
# works here - it is also what launchSettings.json uses for a plain `dotnet run`
PORT=5001
CERT_PEM=cert/local-dev.pem
CERT_KEY=cert/local-dev.key
BUILD=1
FRESH=0

usage() {
    sed -n '2,14p' "$0" | sed 's/^# \?//'
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        -p|--port)  PORT="$2"; shift 2 ;;
        --no-build) BUILD=0; shift ;;
        --fresh)    FRESH=1; shift ;;
        -h|--help)  usage; exit 0 ;;
        *)          echo "unknown option: $1" >&2; usage >&2; exit 1 ;;
    esac
done

cd "$(dirname "$0")"

# Microsoft OAuth credentials: environment wins, otherwise fall back to the project's
# user secrets so nothing sensitive has to live in this script. They are not optional -
# the authentication middleware validates the options on *every* request, so without
# them even /health answers 500.
CLIENT_ID="${Authentication__Microsoft__ClientId:-}"
CLIENT_SECRET="${Authentication__Microsoft__ClientSecret:-}"
if [[ -z "$CLIENT_ID" || -z "$CLIENT_SECRET" ]]; then
    while IFS= read -r line; do
        case "$line" in
            "Authentication:Microsoft:ClientId = "*)     CLIENT_ID="${line#*= }" ;;
            "Authentication:Microsoft:ClientSecret = "*) CLIENT_SECRET="${line#*= }" ;;
        esac
    done < <(dotnet user-secrets list --project mps.csproj 2>/dev/null || true)
fi
if [[ -z "$CLIENT_ID" || -z "$CLIENT_SECRET" ]]; then
    echo "error: no Microsoft OAuth ClientId/ClientSecret found." >&2
    echo "       Set them with 'dotnet user-secrets set Authentication:Microsoft:ClientId <id>'" >&2
    echo "       (see CLAUDE.md > Configuration) or export Authentication__Microsoft__ClientId" >&2
    echo "       and Authentication__Microsoft__ClientSecret before running this." >&2
    exit 1
fi

# Kestrel serves the ASP.NET Core development certificate. It is handed over as PEM
# (Path + KeyPath), so no pfx password is involved, and it is exported into the gitignored
# cert/ directory rather than ~/.aspnet/https so nothing outside the repo is touched.
# cert/server.pfx is a different, long-expired certificate - leave it alone.
if [[ -f "$CERT_PEM" ]] && command -v openssl >/dev/null &&
   ! openssl x509 -in "$CERT_PEM" -noout -checkend 0 >/dev/null 2>&1; then
    echo "the exported dev certificate has expired - re-exporting"
    rm -f "$CERT_PEM" "$CERT_KEY"
fi
if [[ ! -f "$CERT_PEM" || ! -f "$CERT_KEY" ]]; then
    mkdir -p "$(dirname "$CERT_PEM")"
    dotnet dev-certs https --export-path "$CERT_PEM" --format Pem --no-password
fi

if [[ $BUILD -eq 1 ]]; then
    docker build -f Dockerfile.x64 -t "$IMAGE" .
fi

docker rm -f "$NAME" >/dev/null 2>&1 || true

if [[ $FRESH -eq 1 ]]; then
    docker volume rm "$DATA_VOLUME" "$KEYS_VOLUME" >/dev/null 2>&1 || true
    echo "removed the local data and key volumes - migrations will re-seed from scratch"
fi

echo
echo "  app        https://localhost:$PORT"
echo "  health     https://localhost:$PORT/health"
echo "  container  $NAME (docker inspect --format '{{.State.Health.Status}}' $NAME)"
echo
echo "  Sign-in posts back to https://localhost:$PORT/signin-microsoft - that exact URI"
echo "  must be registered on the Microsoft app registration. If the browser warns about"
echo "  the certificate, trust it once with: dotnet dev-certs https --trust"
echo

# HTTPS only, so the image's HEALTHCHECK (plain HTTP on 8080) cannot apply - override it.
# Overriding --health-cmd resets the other health options to their defaults, so the start
# period has to be repeated here. --no-check-certificate because the dev certificate is
# not in the container's trust store.
exec docker run -it --rm --name "$NAME" \
    -p "$PORT:443" \
    -e ASPNETCORE_URLS="https://+:443" \
    -e ASPNETCORE_Kestrel__Certificates__Default__Path=/app/https/cert.pem \
    -e ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/app/https/cert.key \
    -e "Authentication__Microsoft__ClientId=$CLIENT_ID" \
    -e "Authentication__Microsoft__ClientSecret=$CLIENT_SECRET" \
    --health-cmd "wget --no-check-certificate -q --spider https://localhost:443/health || exit 1" \
    --health-start-period 30s \
    --mount "type=bind,src=$PWD/$CERT_PEM,dst=/app/https/cert.pem,ro" \
    --mount "type=bind,src=$PWD/$CERT_KEY,dst=/app/https/cert.key,ro" \
    --mount "type=volume,src=$DATA_VOLUME,dst=/app/data" \
    --mount "type=volume,src=$KEYS_VOLUME,dst=/root/.aspnet/DataProtection-Keys" \
    "$IMAGE"
