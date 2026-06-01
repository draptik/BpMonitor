# Installing BpMonitor on Arch Linux

BpMonitor is distributed as a self-contained single executable — no .NET runtime required.

## Install

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash
```

This downloads the latest release and installs it to `~/.local/bin/bp/bpmonitor`.

## Custom install location

Use flags to override defaults:

| Flag | Default | Description |
| ---- | ------- | ----------- |
| `-b PATH` | `~/.local/bin` | Base directory |
| `-d NAME` | `bp` | Subdirectory under base path |
| `-n NAME` | `bpmonitor` | Executable name |

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -b /usr/local/bin -d bp -n bpmonitor
```

Or via environment variables:

```bash
BASE_PATH=/usr/local/bin INSTALL_DIR_NAME=bp BINARY_NAME=bpmonitor \
  curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash
```

Run with `-h` for full usage:

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -h
```

## Web UI

The web frontend ships as a self-contained bundle on the same release. It needs
no .NET runtime; the single-file executable carries its own `wwwroot/` static
assets and `appsettings.json`.

Install it with the same script, passing `-t web`:

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash -s -- -t web
```

This installs to `~/.local/bin/bpweb/bpmonitor-web`. Run it **from its install
directory** so the bundled `wwwroot/` static assets resolve:

```bash
cd ~/.local/bin/bpweb && ./bpmonitor-web
```

The server binds `http://0.0.0.0:5000` (configured via the bundled
`appsettings.json`). Override the database location with the
`ConnectionStrings__DefaultConnection` environment variable:

```bash
cd ~/.local/bin/bpweb && \
  ConnectionStrings__DefaultConnection="Data Source=$HOME/.local/share/bpmonitor/bpmonitor.db" \
  ./bpmonitor-web
```

### Docker Compose

To run the web app in a container instead, use the example Compose file at
`deploy/docker-compose.yml`. It pulls the prebuilt image published to GitHub
Container Registry (`ghcr.io/draptik/bpmonitor-web`) on each release and
persists the database on a named volume:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

The UI is then served on `http://localhost:5000`. Pin a specific version by
replacing `:latest` with a release tag (e.g.
`ghcr.io/draptik/bpmonitor-web:0.1.11`). Podman users can substitute
`podman compose`, or use the systemd Quadlet units (`deploy/*.container`,
`deploy/*.volume`).

## Helper scripts

`docs/scripts/` contains convenience scripts for managing a local installation.

### Install and configure

```bash
IMPORT_FILE_LOCATION=~/documents/health bash docs/scripts/install.sh
```

Runs `install.sh` and patches `appsettings.json` to set `MarkdownDirectory` to the given path. Defaults to `.` if `IMPORT_FILE_LOCATION` is not set.

### Remove local installation

```bash
bash docs/scripts/clean.sh
```

Removes the `~/.local/bin/bp/` directory.

## Creating a new release

Push a version tag on `main` to trigger the release workflow:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The workflow builds the executable and publishes a GitHub Release with auto-generated release notes.
