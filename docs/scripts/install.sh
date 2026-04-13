#!/usr/bin/env bash
set -euo pipefail

IMPORT_FILE_LOCATION="${IMPORT_FILE_LOCATION:-.}"
INSTALL_DIR="${HOME}/.local/bin/bp"

curl -fsSL https://raw.githubusercontent.com/draptik/BpMonitor/main/install.sh | bash

sed -i "s|\"MarkdownDirectory\": \".\"|\"MarkdownDirectory\": \"${IMPORT_FILE_LOCATION}\"|" "${INSTALL_DIR}/appsettings.json"
