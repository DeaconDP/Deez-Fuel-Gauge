#!/bin/bash
cd "$(dirname "$0")"
REPO_ROOT="$(pwd)"
export PATH="/opt/homebrew/bin:/usr/local/bin:$HOME/.dotnet:$HOME/.local/bin:$PATH"

LOG_DIR="$HOME/Library/Logs/DeezFuelGauge"
LOG_FILE="$LOG_DIR/setup.log"
PACKAGE_SCRIPT="$REPO_ROOT/scripts/package-macos-app.sh"

show_message() {
    /usr/bin/osascript -e "display dialog \"$1\" buttons {\"OK\"} default button \"OK\" with title \"Deez Fuel Gauge\"" >/dev/null 2>&1 || true
}

mkdir -p "$LOG_DIR"
echo "" | tee -a "$LOG_FILE"
echo "=== run.command $(date) ===" | tee -a "$LOG_FILE"

if ! /bin/bash "$PACKAGE_SCRIPT" 2>&1 | tee -a "$LOG_FILE"; then
    echo "Run failed. Details were saved to $LOG_FILE." | tee -a "$LOG_FILE"
    show_message "Run failed. Details were saved to $LOG_FILE."
    echo
    echo "Press Enter to close."
    read -r
    exit 1
fi

echo "Done." | tee -a "$LOG_FILE"
