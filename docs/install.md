# Installing BpMonitor

BpMonitor Web ships as a self-contained Linux binary — no .NET runtime required.

## Quick install

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -t web
```

This installs the latest release to `~/.local/bin/bpweb/bpmonitor-web`. Run it
**from its install directory** so the bundled `wwwroot/` static assets resolve:

```bash
cd ~/.local/bin/bpweb && ./bpmonitor-web
```

The server binds `http://0.0.0.0:5000`.

## Custom install location

Use flags to override defaults:

| Flag | Default | Description |
| ---- | ------- | ----------- |
| `-b PATH` | `~/.local/bin` | Base directory |
| `-d NAME` | `bpweb` | Subdirectory under base path |
| `-n NAME` | `bpmonitor-web` | Executable name |

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -t web -b /usr/local/bin -d bpweb
```

Or via environment variables:

```bash
BASE_PATH=/usr/local/bin INSTALL_DIR_NAME=bpweb \
  curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -t web
```

## Configuration

- **Database** — defaults to `Data Source=<install-dir>/bpmonitor.db`; override with `ConnectionStrings__DefaultConnection`.
- **Bind address / port** — defaults to `http://0.0.0.0:5000`; configured via `appsettings.json` (takes precedence over `ASPNETCORE_URLS`).

## Docker Compose

To run as a container instead (pulls the prebuilt image from GitHub Container Registry):

```bash
docker compose -f deploy/docker-compose.yml up -d
```

See [deploy/README.md](../deploy/README.md) for container configuration, Podman, and systemd Quadlet instructions.

## Helper scripts

`docs/scripts/` contains convenience scripts for managing a local installation:

- **Install and configure:** patches `appsettings.json` with a custom `MarkdownDirectory`:

  ```bash
  IMPORT_FILE_LOCATION=~/documents/health bash docs/scripts/install.sh
  ```

- **Remove local installation:**

  ```bash
  bash docs/scripts/clean.sh
  ```
