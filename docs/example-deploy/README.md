# Deploying BpMonitor.Web

The web UI runs as a long-lived container on the homelab VM. It binds
`0.0.0.0:5000` and keeps its SQLite database on a mounted volume, so it is
reachable from any device on the LAN (including a phone browser) and survives
redeploys.

## Image

Every release publishes an image to GitHub Container Registry:
`ghcr.io/draptik/bpmonitor-web`. `:latest` follows the newest stable release;
pin a version (e.g. `:1.14.0`) to control when you upgrade. Release candidates
are only published under their own tag (e.g. `:1.14.0-rc1`).

### Alternative: build locally

From the repository `code/` directory:

```sh
podman build -t localhost/bpmonitor-web:latest -f BpMonitor.Web/Containerfile .
```

> The build context is `code/` (the `.` above) because `BpMonitor.Web` references
> the sibling `Core`, `Data`, `Charts` and `Export` projects.

Then use `localhost/bpmonitor-web:latest` instead of the GHCR image below.

## Run ad-hoc

```sh
podman run -d --name bpmonitor-web \
  -p 5000:5000 \
  -v bpmonitor-data:/data:U \
  ghcr.io/draptik/bpmonitor-web:latest
```

Then open `http://<vm-ip>:5000`.

The container runs as the unprivileged `app` user (UID 1654), not root. The
`:U` volume option makes Podman hand `/data` to that user.

## Upgrading from an image that ran as root

Images before this change ran as root, so an existing database and `keys/`
directory are owned by root, and the app can read them but no longer save
readings. `/health` still reports healthy in that state, so fix the
ownership once before starting the new image:

- **Podman / Quadlet:** nothing to do if you use the `:U` volume option shown above.
- **Docker Compose (bind mount `./data`):** `sudo chown -R 1654:1654 data`
- **Docker named volume:**
  `docker run --rm --user root --entrypoint chown -v <volume>:/data ghcr.io/draptik/bpmonitor-web:latest -R 1654:1654 /data`

## Run as a systemd service (Quadlet)

Copy the unit files to the systemd generator directory:

- rootful:  `/etc/containers/systemd/`
- rootless: `~/.config/containers/systemd/`

```sh
cp bpmonitor-web.container bpmonitor-data.volume <systemd-dir>/
systemctl daemon-reload          # add --user for rootless
systemctl start bpmonitor-web
```

## Health check

`GET /health` is an anonymous endpoint that checks whether the SQLite database is
reachable — useful for catching a missing or unwritable `/data` volume. It returns
`200` with `{"status":"healthy","version":"…","database":"connected"}` when the
database can be opened, or `503` with `"status":"unhealthy"` otherwise. Both the
Containerfile's `HEALTHCHECK` and the Quadlet unit above poll it.

## Configuration

- **Bind address / port** — defaults to `http://0.0.0.0:5000`; configured via
  `appsettings.json` `Urls` key (takes precedence over `ASPNETCORE_URLS`).
- **Database location** — defaults to `Data Source=/data/bpmonitor.db`; override
  with `ConnectionStrings__DefaultConnection`.
- **Validation ranges** — read from `appsettings.json`; override individual
  bounds with `ReadingRanges__*` environment variables.

> **Security:** access is protected by per-member cookie authentication (each
> family member claims their account with a one-time setup code and sets a password —
> see [First sign-in](../install.md#first-sign-in)). Avoid exposing port 5000 directly
> to the internet — use a reverse proxy with TLS for internet-facing deployments.
