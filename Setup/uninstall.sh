#!/usr/bin/env bash
set -euo pipefail

program="$HOME/Documents/FPEngine/FishPaper"
wallpapers="$HOME/.config/autostart/wallpapers"

read -r -p "Uninstall FishPaper and delete its wallpapers folder? (y/N): " answer
if [[ ! "$answer" =~ ^[Yy]$ ]]; then
    echo "Uninstall cancelled."
    exit 0
fi

rm -f -- "$program"
rmdir -- "$HOME/Documents/FPEngine" 2>/dev/null || true
rm -rf -- "$wallpapers"

echo "FishPaper and its wallpapers folder have been removed."

read -r -p "Delete this uninstall script too? (y/N): " answer
if [[ "$answer" =~ ^[Yy]$ ]]; then
    script_path="$(realpath -- "${BASH_SOURCE[0]}")"
    rm -f -- "$script_path"
    echo "Uninstall script removed."
fi
