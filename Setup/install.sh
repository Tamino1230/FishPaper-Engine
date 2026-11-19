#!/usr/bin/env bash

set -Eeuo pipefail

on_error() {
    echo "Error: command failed on line $1: $2" >&2
}
trap 'on_error "$LINENO" "$BASH_COMMAND"' ERR

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

copy_file() {
    local source="$1"
    local destination="$2"

    if [[ ! -f "$source" ]]; then
        echo "Error: source file not found: $source" >&2
        return 1
    fi

    mkdir -p -- "$(dirname -- "$destination")"
    cp -- "$source" "$destination"
    echo "Copied: $source -> $destination"
}

copy_file "$script_dir/FishPaper" \
    "$HOME/Documents/FPEngine/FishPaper"

copy_file "$script_dir/libglfw.so.3" \
    "$HOME/Documents/FPEngine/libglfw.so.3"

copy_file "$script_dir/FishPaperEngine-Shortcut.desktop" \
    "$HOME/.config/autostart/FishPaperEngine-Shortcut.desktop"
    
copy_file "$script_dir/uninstall.sh" \
    "$HOME/Documents/FPEngine/uninstall.sh"

wallpapers="$HOME/Documents/FPEngine/wallpapers"
mkdir -p -- "$wallpapers"

if command -v xdg-open >/dev/null 2>&1; then
    xdg-open "$wallpapers" || {
        echo "Error: couldn't open $wallpapers" >&2
        exit 1
    }
else
    echo "Error: xdg-open is not installed." >&2
    exit 1
fi

read -r -p "Start FishPaper? (y/N): " answer
if [[ "$answer" =~ ^[Yy]$ ]]; then
    program="$HOME/Documents/FPEngine/FishPaper"

    if [[ ! -x "$program" ]]; then
        chmod +x "$program" || {
            echo "Error: couldn't make FishPaper executable." >&2
            exit 1
        }
    fi

    echo "Please put two wallpaper files in: $wallpapers"
    read -r -p "Press Enter when both wallpapers are in the folder..."

    "$program" || {
        status=$?
        echo "Error: FishPaper exited with status $status" >&2
        exit "$status"
    }
else
    echo "FishPaper was not started."
fi
