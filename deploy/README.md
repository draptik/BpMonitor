# Deploying BpMonitor.Web

The web UI runs as a long-lived container on the homelab VM. It binds
`0.0.0.0:5000` and keeps its SQLite database on a mounted volume, so it is
reachable from any device on the LAN (including a phone browser) and survives
redeploys.

## Build the image

From the repository `code/` directory:

```sh
podman build -t localhost/bpmonitor-web:latest -f BpMonitor.Web/Containerfile .
```

> The build context is `code/` (the `.` above) because `BpMonitor.Web` references
> the sibling `Core`, `Data` and `Charts` projects.

## Run ad-hoc

```sh
podman run -d --name bpmonitor-web \
  -p 5000:5000 \
  -v bpmonitor-data:/data \
  localhost/bpmonitor-web:latest
```

Then open `http://<vm-ip>:5000`.

## Run as a systemd service (Quadlet)

Copy the unit files to the systemd generator directory:

- rootful:  `/etc/containers/systemd/`
- rootless: `~/.config/containers/systemd/`

```sh
cp bpmonitor-web.container bpmonitor-data.volume <systemd-dir>/
systemctl daemon-reload          # add --user for rootless
systemctl start bpmonitor-web
```

## Configuration

- **Bind address / port** — defaults to `http://0.0.0.0:5000`; override with
  `ASPNETCORE_URLS`.
- **Database location** — defaults to `Data Source=/data/bpmonitor.db`; override
  with `ConnectionStrings__DefaultConnection`.
- **Validation ranges** — read from `appsettings.json`; override individual
  bounds with `ReadingRanges__*` environment variables.

> Health data is served unauthenticated; keep it on a trusted LAN (or front it
> with a reverse proxy / auth) rather than exposing it to the internet.
