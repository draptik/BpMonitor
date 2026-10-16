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

- **Database** — defaults to `Data Source=<install-dir>/bpmonitor.db`; override with `ConnectionStrings__DefaultConnection`.
- **Bind address / port** — defaults to `http://0.0.0.0:5000`; configured via `appsettings.json` (takes precedence over `ASPNETCORE_URLS`).
- **Health check** — `GET /health` reports database reachability (`200`/`503`); see [docs/example-deploy/README.md](example-deploy/README.md#health-check) for details.
- **"Remember me" duration** — `BpMonitor__RememberMeDays` (default `30`, clamped to 1–400) controls how long a "remember me" login stays signed in.
- **Data Protection key persistence** — `BpMonitor__DataProtectionKeyPath` points at a directory to store the keys that encrypt the auth cookie. Unset (the manual-install default), keys live under the user's home directory and survive fine across restarts of the same install. The container image sets this to `/data/keys` on the same volume as the database, so keys — and any "remember me" sessions — survive a container recreation; if you're running an older container image, upgrading will invalidate existing sessions once (a one-time logout) as keys move to the persisted location.

## Docker Compose

To run as a container instead (pulls the prebuilt image from GitHub Container Registry):

```bash
docker compose -f docs/example-deploy/docker-compose.yml up -d
```

See [docs/example-deploy/README.md](example-deploy/README.md) for container configuration, Podman, and systemd Quadlet instructions.
