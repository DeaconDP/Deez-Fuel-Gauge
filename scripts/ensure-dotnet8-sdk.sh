#!/bin/bash
# Resolves a dotnet executable with the .NET 8 SDK available.
# Installs SDK 8.0 to ~/.dotnet via dotnet-install.sh when needed.

set -euo pipefail

INSTALL_SCRIPT_URL="https://dot.net/v1/dotnet-install.sh"

has_dotnet8_sdk() {
    local dotnet="$1"
    "$dotnet" --list-sdks 2>/dev/null | grep -qE '^8\.'
}

list_dotnet_candidates() {
    export PATH="/usr/local/share/dotnet:/opt/homebrew/bin:/usr/local/bin:$HOME/.dotnet:$HOME/.dotnet/tools:${PATH:-}"

    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
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
        fi
    done
}

install_dotnet8_sdk() {
    local install_dir="$HOME/.dotnet"
    local script_path
    script_path="$(mktemp "${TMPDIR:-/tmp}/dotnet-install.XXXXXX.sh")"

    mkdir -p "$install_dir"
    curl -fsSL "$INSTALL_SCRIPT_URL" -o "$script_path"
    chmod +x "$script_path"
    /bin/bash "$script_path" --channel 8.0 --install-dir "$install_dir" >&2
    rm -f "$script_path"

    echo "$install_dir/dotnet"
}

resolve_dotnet8() {
    local candidate
    local seen=""

    while IFS= read -r candidate; do
        [[ -n "$candidate" ]] || continue
        case ":$seen:" in
            *":$candidate:"*) continue ;;
        esac
        seen="${seen:+$seen:}$candidate"

        if has_dotnet8_sdk "$candidate"; then
            echo "$candidate"
            return 0
        fi
    done < <(list_dotnet_candidates | awk '!seen[$0]++')

    install_dotnet8_sdk
}

DOTNET="$(resolve_dotnet8)"
export DOTNET
