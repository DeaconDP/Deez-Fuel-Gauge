#!/bin/bash

# Self-update note: update_to_latest may replace this file on disk while it is
# running. Bash reads scripts incrementally, so all logic lives in functions
# and the only top-level statements are definitions plus the single
# `main "$@"` line at the bottom — a fully parsed function is immune to the
# file changing underneath it, and after an update we exec the fresh copy.

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
LOG_DIR="$HOME/Library/Logs/DeezFuelGauge"
LOG_FILE="$LOG_DIR/setup.log"

log() {
    echo "$1" | tee -a "$LOG_FILE"
}

show_message() {
    /usr/bin/osascript -e "display dialog \"$1\" buttons {\"OK\"} default button \"OK\" with title \"Deez Fuel Gauge\"" >/dev/null 2>&1 || true
}

# Brings this checkout to the latest version: fast-forwards the current
# branch, and if the branch no longer exists on GitHub (it was merged), hops
# back to the default branch so setup never keeps rebuilding stale code.
# Returns 0 only when HEAD moved, so the caller knows to relaunch with the
# updated scripts. Any failure is non-fatal: setup continues with the
# version on disk.
update_to_latest() {
    if [[ "${DEEZ_SETUP_UPDATED:-0}" == "1" ]]; then
        return 1
    fi

    if ! command -v git >/dev/null 2>&1; then
        log "git is not installed; skipping the update check."
        return 1
    fi

    if [[ ! -e "$REPO_ROOT/.git" ]]; then
        log "This folder is not a git checkout; skipping the update check."
        return 1
    fi

    local branch before after default
    branch="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD)"
    if [[ "$branch" == "HEAD" ]]; then
        log "Detached checkout; skipping the update check."
        return 1
    fi

    log "Checking GitHub for the latest version..."
    if ! git -C "$REPO_ROOT" fetch --prune origin 2>&1 | tee -a "$LOG_FILE"; then
        log "Could not reach GitHub (offline?). Continuing with the current version."
        return 1
    fi

    before="$(git -C "$REPO_ROOT" rev-parse HEAD)"

    default="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref origin/HEAD 2>/dev/null)"
    default="${default#origin/}"
    [[ -z "$default" ]] && default="main"

    # Branch matching assumes the cursor/Claude workflow: local branches
    # share their name with the GitHub branch they came from.
    if ! git -C "$REPO_ROOT" rev-parse --verify --quiet "origin/$branch" >/dev/null &&
        git -C "$REPO_ROOT" rev-parse --verify --quiet "origin/$default" >/dev/null &&
        [[ "$branch" != "$default" ]]; then
        if [[ -n "$(git -C "$REPO_ROOT" status --porcelain)" ]]; then
            log "Branch '$branch' is not on GitHub and has local edits; running that work-in-progress."
        elif [[ -n "$(git -C "$REPO_ROOT" log --oneline "origin/$default..$branch")" ]]; then
            log "Branch '$branch' has commits that are not on '$default' yet; running that work-in-progress."
        else
            log "Branch '$branch' is fully merged; switching to '$default'..."
            if ! git -C "$REPO_ROOT" checkout "$default" 2>&1 | tee -a "$LOG_FILE"; then
                log "Could not switch to '$default'. Continuing with the current version."
                return 1
            fi
            branch="$default"
        fi
    fi

    if git -C "$REPO_ROOT" rev-parse --verify --quiet "origin/$branch" >/dev/null; then
        if ! git -C "$REPO_ROOT" merge --ff-only "origin/$branch" 2>&1 | tee -a "$LOG_FILE"; then
            log "Could not fast-forward (local changes in the way, or the branch has diverged). Continuing with the current version."
            return 1
        fi
    fi

    after="$(git -C "$REPO_ROOT" rev-parse HEAD)"
    [[ "$before" != "$after" ]]
}

# e.g. " (main @ 1af526a)" — empty when git is unavailable.
version_label() {
    command -v git >/dev/null 2>&1 || return 0
    [[ -e "$REPO_ROOT/.git" ]] || return 0
    local branch sha
    branch="$(git -C "$REPO_ROOT" rev-parse --abbrev-ref HEAD 2>/dev/null)" || return 0
    sha="$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null)" || return 0
    echo " ($branch @ $sha)"
}

main() {
    mkdir -p "$LOG_DIR"
    set -o pipefail

    log ""
    log "=== setup-and-run.command $(date) ==="

    if update_to_latest; then
        # New commits may include changes to this very script, so hand off to
        # the freshly pulled copy instead of continuing with the stale one.
        log "Updated to the latest version; restarting setup with the new scripts..."
        DEEZ_SETUP_UPDATED=1 exec /bin/bash "$REPO_ROOT/setup-and-run.command"
    fi

    local native_launcher="$REPO_ROOT/setup-and-run.app/Contents/MacOS/setup-and-run-native"
    local package_script="$REPO_ROOT/scripts/package-macos-app.sh"

    if [[ -x "$native_launcher" ]]; then
        log "Using native setup launcher$(version_label)."
        exec >>"$LOG_FILE" 2>&1 "$native_launcher"
    fi

    log "Building Deez Fuel Gauge$(version_label)..."
    if ! /bin/bash "$package_script" 2>&1 | tee -a "$LOG_FILE"; then
        log "Setup failed."
        show_message "Setup failed. Details were saved to $LOG_FILE."
        exit 1
    fi

    log "Setup finished."
}

# Single line on purpose (see self-update note above). The guard also lets
# tests source this file to exercise individual functions without running main.
if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then main "$@"; fi
