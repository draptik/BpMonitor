#!/usr/bin/env bash
# install.sh — Download and install the latest BpMonitor web release from GitHub.
#
# Usage:
#   ./install.sh [OPTIONS]
#
# Options:
#   -b PATH   Base directory for installation      (default: ~/.local/bin)
#   -d NAME   Subdirectory under base path         (default: bpweb)
#   -n NAME   Name of the installed executable     (default: bpmonitor-web)
#   -h        Show this help message
#
# Options take precedence over environment variables of the same meaning.
# The binary is installed to: BASE_PATH/INSTALL_DIR_NAME/BINARY_NAME
#
# BpMonitor.Web is distributed as a self-contained tarball
# (bpmonitor-web-linux-x64.tar.gz) bundling the single-file binary,
# appsettings.json, and its wwwroot/ static assets. Run the binary from its
# install directory so static assets resolve correctly, e.g.:
#   cd ~/.local/bin/bpweb && ./bpmonitor-web
#
# Examples:
#   ./install.sh
#   ./install.sh -b /opt/local/bin
#   ./install.sh -d bpmon -n bp
set -euo pipefail

REPO="draptik/BpMonitor"
DEFAULT_BINARY="bpmonitor-web"
BASE_PATH="${BASE_PATH:-$HOME/.local/bin}"
# Left empty so the default below only applies when the user
# did not override it via an option or environment variable.
INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-}"
BINARY_NAME="${BINARY_NAME:-}"

usage() {
  sed -n '/^# install\.sh/,/^[^#]/{ /^[^#]/d; s/^# \?//; p }' "$0"
  exit 0
}

while getopts ":b:d:n:h" opt; do
  case $opt in
    b) BASE_PATH="$OPTARG" ;;
    d) INSTALL_DIR_NAME="$OPTARG" ;;
    n) BINARY_NAME="$OPTARG" ;;
    h) usage ;;
    :) echo "Option -$OPTARG requires an argument." >&2; exit 1 ;;
    \?) echo "Unknown option: -$OPTARG" >&2; exit 1 ;;
  esac
done

INSTALL_DIR_NAME="${INSTALL_DIR_NAME:-bpweb}"
ARTIFACT_NAME="$DEFAULT_BINARY-linux-x64.tar.gz"
BINARY_NAME="${BINARY_NAME:-$DEFAULT_BINARY}"
INSTALL_DIR="$BASE_PATH/$INSTALL_DIR_NAME"
INSTALL_PATH="$INSTALL_DIR/$BINARY_NAME"

LATEST=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" | grep tag_name | cut -d'"' -f4)
DOWNLOAD_URL="https://github.com/$REPO/releases/download/$LATEST/$ARTIFACT_NAME"

mkdir -p "$INSTALL_DIR"

# Ships as a tarball (binary + appsettings.json + wwwroot/).
tarball=$(mktemp)
curl -fsSL "$DOWNLOAD_URL" -o "$tarball"
tar -xzf "$tarball" -C "$INSTALL_DIR"
rm -f "$tarball"

# Honour a custom binary name by renaming the extracted executable.
if [ "$BINARY_NAME" != "$DEFAULT_BINARY" ]; then
  mv "$INSTALL_DIR/$DEFAULT_BINARY" "$INSTALL_PATH"
fi
chmod +x "$INSTALL_PATH"

echo "Installed $BINARY_NAME $LATEST to $INSTALL_PATH"
echo "Run it from its install directory so static assets resolve:"
echo "  cd $INSTALL_DIR && ./$BINARY_NAME"
