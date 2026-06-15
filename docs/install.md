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

## Configuration

- **Database** — defaults to `Data Source=<install-dir>/bpmonitor.db`; override with `ConnectionStrings__DefaultConnection`.
- **Bind address / port** — defaults to `http://0.0.0.0:5000`; configured via `appsettings.json` (takes precedence over `ASPNETCORE_URLS`).

## Docker Compose

To run as a container instead (pulls the prebuilt image from GitHub Container Registry):

```bash
docker compose -f docs/example-deploy/docker-compose.yml up -d
```

See [docs/example-deploy/README.md](example-deploy/README.md) for container configuration, Podman, and systemd Quadlet instructions.
