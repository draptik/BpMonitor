# Installing BpMonitor

BpMonitor Web ships as a self-contained Linux binary — no .NET runtime required.

## Manual install (tarball)

1. Download the latest release tarball from the [GitHub Releases](https://github.com/draptik/BpMonitor/releases/latest) page:

   ```bash
   curl -fsSL -o bpmonitor-web-linux-x64.tar.gz \
     https://github.com/draptik/BpMonitor/releases/latest/download/bpmonitor-web-linux-x64.tar.gz
   ```

2. Extract to your install directory:

   ```bash
   mkdir -p ~/.local/bin/bpweb
   tar -xzf bpmonitor-web-linux-x64.tar.gz -C ~/.local/bin/bpweb
   chmod +x ~/.local/bin/bpweb/bpmonitor-web
   ```

3. Run **from the install directory** so the bundled `wwwroot/` static assets resolve:

   ```bash
   cd ~/.local/bin/bpweb && ./bpmonitor-web
   ```

The server binds `http://0.0.0.0:5000`.

## First sign-in

On first start, BpMonitor creates an admin member named `Me` and writes a one-time setup code
for it to the log:

```text
[INF] Setup code for member Me (Id=1): K7QX-M2PA
```

Open `http://<host>:5000/login`, sign in as `Me`, enter the setup code and choose a password.
With Docker Compose, find the code with:

```bash
docker compose -f docs/example-deploy/docker-compose.yml logs bpmonitor-web | grep "Setup code"
```

Add the rest of the family on the **Members** page. It shows each new member's setup code once —
pass it on, and they use it to set their own password. Resetting a member's password shows a new
code the same way. A code stays valid until it's used or replaced by another reset.

**Upgrading from a version without setup codes:** members who haven't set a password yet get a
setup code in the log on the first start after the upgrade.

## Configuration

All settings live in `appsettings.json`; override any of them with environment variables
(`:` becomes `__`).

- **Database** — `ConnectionStrings__DefaultConnection`; defaults to `Data Source=<install-dir>/bpmonitor.db` (container: `/data/bpmonitor.db`).
- **Bind address / port** — `Urls` key, default `http://0.0.0.0:5000` (takes precedence over `ASPNETCORE_URLS`).
- **Validation ranges** — override individual bounds with `ReadingRanges__*`.
- **Default language** — `BpMonitor__DefaultLanguage` (`en` or `de`, default `en`) for visitors whose member setting, cookie and browser don't pick one.
- **Secure cookies** — set `BpMonitor__SecureCookies=true` when serving over HTTPS (e.g. behind a TLS reverse proxy) so the auth cookie is only sent over TLS.
- **"Remember me" duration** — `BpMonitor__RememberMeDays` (default `30`, clamped to 1–400).
- **Data Protection keys** — `BpMonitor__DataProtectionKeyPath` sets where the keys that encrypt the auth cookie live. Unset, they go under the user's home directory, which is fine for a manual install. The container image sets `/data/keys`, next to the database, so "remember me" sessions survive a container recreation.
- **Health check** — `GET /health` reports database reachability (`200`/`503`); see [Health check](example-deploy/README.md#health-check).

## Docker Compose

To run as a container instead (pulls the prebuilt image from GitHub Container Registry):

```bash
docker compose -f docs/example-deploy/docker-compose.yml up -d
```

See [docs/example-deploy/README.md](example-deploy/README.md) for container configuration, Podman, and systemd Quadlet instructions.
