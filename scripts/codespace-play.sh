#!/usr/bin/env bash
# Builds the web client and runs the game server with both transports: TCP on 7777 for the desktop client,
# and on 7778 the WebSocket game socket plus the built web client itself. Made for a GitHub Codespace, works
# in any Linux or macOS shell with the .NET 10 SDK and Node 22. See docs/infrastructure/codespaces.md.
#
#   scripts/codespace-play.sh [--public] [server options such as --ws-port 7778 --log Debug]
#
# --public asks the GitHub CLI to make the web port public in this Codespace, so a browser that is not
# signed in to GitHub (or the client on GitHub Pages) can reach it. Anyone with the address can then play.
set -euo pipefail
cd "$(dirname "$0")/.."

public=false
port=7778
args=()
while [ $# -gt 0 ]; do
  case "$1" in
    --public)
      public=true
      ;;
    --ws-port)
      port="$2"
      args+=("$1" "$2")
      shift
      ;;
    *)
      args+=("$1")
      ;;
  esac
  shift
done

if [ ! -d web/node_modules ]; then
  npm --prefix web ci
fi
npm --prefix web run build

if [ -n "${CODESPACE_NAME:-}" ]; then
  url="https://${CODESPACE_NAME}-${port}.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}/"
  if [ "$public" = true ]; then
    if gh codespace ports visibility "${port}:public" -c "$CODESPACE_NAME" >/dev/null 2>&1; then
      echo "Port ${port} is public: anyone with the address can play."
    else
      echo "Could not change the port visibility from here; in the Ports panel, right-click port ${port} and choose Port Visibility, Public."
    fi
  fi
  echo
  echo "Web client: ${url}"
  echo "If the page does not load, make port ${port} public in the Ports panel (right-click, Port Visibility, Public)."
  echo "The client on GitHub Pages connects to: wss://${CODESPACE_NAME}-${port}.${GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN:-app.github.dev}/ws"
else
  url="http://localhost:${port}/"
  echo
  echo "Web client: ${url}"
fi
echo "Stop the server with Ctrl+C. Accounts and characters stay in data/."
echo

exec dotnet run --project src/HollowWake.Server.Host -- ${args[@]+"${args[@]}"}
