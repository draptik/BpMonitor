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
