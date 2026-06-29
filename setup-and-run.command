#!/bin/bash

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
LOG_DIR="$HOME/Library/Logs/DeezFuelGauge"
LOG_FILE="$LOG_DIR/setup.log"
NATIVE_LAUNCHER="$REPO_ROOT/setup-and-run.app/Contents/MacOS/setup-and-run-native"
PACKAGE_SCRIPT="$REPO_ROOT/scripts/package-macos-app.sh"

mkdir -p "$LOG_DIR"

log() {
    echo "$1" | tee -a "$LOG_FILE"
}

show_message() {
    /usr/bin/osascript -e "display dialog \"$1\" buttons {\"OK\"} default button \"OK\" with title \"Deez Fuel Gauge\"" >/dev/null 2>&1 || true
}

log ""
log "=== setup-and-run.command $(date) ==="

if [[ -x "$NATIVE_LAUNCHER" ]]; then
    log "Using native setup launcher."
    exec >>"$LOG_FILE" 2>&1 "$NATIVE_LAUNCHER"
fi

log "Building Deez Fuel Gauge..."
set -o pipefail
if ! /bin/bash "$PACKAGE_SCRIPT" 2>&1 | tee -a "$LOG_FILE"; then
    log "Setup failed."
    show_message "Setup failed. Details were saved to $LOG_FILE."
    exit 1
fi

log "Setup finished."
