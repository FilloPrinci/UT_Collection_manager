#!/usr/bin/env bash
# Installs UT Launcher for the current user: no root required, no package manager involved.
# Run this script from inside the extracted utlauncher-linux-x64 folder.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DATA_HOME="${XDG_DATA_HOME:-${HOME}/.local/share}"
INSTALL_DIR="${DATA_HOME}/UTLauncher"
BIN_DIR="${HOME}/.local/bin"
DESKTOP_DIR="${DATA_HOME}/applications"

if [[ ! -d "${SCRIPT_DIR}/app" ]]; then
  echo "Error: run this script from inside the extracted utlauncher-linux-x64 folder." >&2
  exit 1
fi

echo "Installing UT Launcher to ${INSTALL_DIR}"
mkdir -p "${INSTALL_DIR}"
cp -rf "${SCRIPT_DIR}/app/." "${INSTALL_DIR}/"
chmod +x "${INSTALL_DIR}/UTLauncher.App"

if [[ -f "${SCRIPT_DIR}/cli/UTLauncher.Cli" ]]; then
  cp -f "${SCRIPT_DIR}/cli/UTLauncher.Cli" "${INSTALL_DIR}/utlauncher-cli"
  chmod +x "${INSTALL_DIR}/utlauncher-cli"
fi

mkdir -p "${BIN_DIR}"
ln -sf "${INSTALL_DIR}/UTLauncher.App" "${BIN_DIR}/utlauncher"
if [[ -f "${INSTALL_DIR}/utlauncher-cli" ]]; then
  ln -sf "${INSTALL_DIR}/utlauncher-cli" "${BIN_DIR}/utlauncher-cli"
fi

mkdir -p "${DESKTOP_DIR}"
cat >"${DESKTOP_DIR}/utlauncher.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=UT Launcher
Comment=Launcher for Unreal Tournament 99, 2004 and 4
Exec=${INSTALL_DIR}/UTLauncher.App
Icon=${INSTALL_DIR}/icon.png
Terminal=false
Categories=Game;
EOF

echo "Done."
echo "Launch it from your application menu, or run: ${BIN_DIR}/utlauncher"
if [[ ":$PATH:" != *":${BIN_DIR}:"* ]]; then
  echo "Note: ${BIN_DIR} is not on your PATH yet; add it to your shell profile to use 'utlauncher' directly."
fi
