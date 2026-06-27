#!/bin/bash

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
LOG_DIR="$HOME/Library/Logs/CursorUsageWidget"
LOG_FILE="$LOG_DIR/setup.log"
NATIVE_LAUNCHER="$REPO_ROOT/setup-and-run.app/Contents/MacOS/setup-and-run-native"
PACKAGE_SCRIPT="$REPO_ROOT/scripts/package-macos-app.sh"

mkdir -p "$LOG_DIR"

log() {
    echo "$1" | tee -a "$LOG_FILE"
}

show_message() {
    /usr/bin/osascript -e "display dialog \"$1\" buttons {\"OK\"} default button \"OK\" with title \"Cursor Usage Widget\"" >/dev/null 2>&1 || true
}

find_dotnet() {
    export PATH="/usr/local/share/dotnet:/opt/homebrew/bin:/usr/local/bin:$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"

    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
        return 0
    fi

    local candidate
    for candidate in \
        "$HOME/.dotnet/dotnet" \
        "/usr/local/share/dotnet/dotnet" \
        "/opt/homebrew/bin/dotnet" \
        "/usr/local/bin/dotnet"
    do
        if [[ -x "$candidate" ]]; then
            echo "$candidate"
            return 0
        fi
    done

    return 1
}

log ""
log "=== setup-and-run.command $(date) ==="

if [[ -x "$NATIVE_LAUNCHER" ]]; then
    log "Using native setup launcher."
    exec >>"$LOG_FILE" 2>&1 "$NATIVE_LAUNCHER"
fi

DOTNET="$(find_dotnet || true)"
if [[ -z "$DOTNET" ]]; then
    log ".NET 8 SDK not found."
    show_message "The .NET 8 SDK is required. We opened the download page in your browser. Install it, then double-click setup-and-run again."
    open "https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

log "Building Cursor Usage Widget..."
set -o pipefail
if ! DOTNET="$DOTNET" /bin/bash "$PACKAGE_SCRIPT" 2>&1 | tee -a "$LOG_FILE"; then
    log "Setup failed."
    show_message "Setup failed. Details were saved to $LOG_FILE."
    exit 1
fi

log "Setup finished."
