# Installing BpMonitor on Arch Linux

BpMonitor is distributed as a self-contained single executable — no .NET runtime required.

## Install

```bash
curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash
```

This downloads the latest release and installs it to `~/.local/bin/bpmonitor`.

## Custom install path

```bash
INSTALL_PATH=/usr/local/bin/bpmonitor curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash
```

## Creating a new release

Push a version tag on `main` to trigger the release workflow:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The workflow builds the executable and publishes a GitHub Release with auto-generated release notes.
