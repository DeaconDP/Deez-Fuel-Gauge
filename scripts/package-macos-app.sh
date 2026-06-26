#!/bin/bash

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET="${DOTNET:-}"

if [[ -z "$DOTNET" ]]; then
    for candidate in \
        "$(command -v dotnet 2>/dev/null || true)" \
        "$HOME/.dotnet/dotnet" \
        "/usr/local/share/dotnet/dotnet" \
        "/opt/homebrew/bin/dotnet" \
        "/usr/local/bin/dotnet"
    do
        if [[ -n "$candidate" && -x "$candidate" ]]; then
            DOTNET="$candidate"
            break
        fi
    done
fi

if [[ -z "$DOTNET" ]]; then
    echo ".NET 8 SDK not found." >&2
    exit 1
fi

exec "$DOTNET" run --project "$REPO_ROOT/CursorUsageWidget.Setup/CursorUsageWidget.Setup.csproj" -c Release --nologo
