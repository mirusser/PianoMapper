#!/usr/bin/env bash
# Runs PianoMapper against the headless Hyprland Wayland socket on archie.
# See docs/remote-access.md for the VNC/audio setup needed to see and hear it.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

export WAYLAND_DISPLAY="${WAYLAND_DISPLAY:-wayland-1}"

exec dotnet run --project PianoMapper "$@"
