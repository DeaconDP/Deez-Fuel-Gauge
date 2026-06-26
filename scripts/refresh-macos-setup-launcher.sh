#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LAUNCHER_DIR="$REPO_ROOT/setup-and-run.app/Contents/MacOS"
SETUP_PROJECT="$REPO_ROOT/CursorUsageWidget.Setup/CursorUsageWidget.Setup.csproj"
ARM64_OUT="$LAUNCHER_DIR/publish-arm64"
X64_OUT="$LAUNCHER_DIR/publish-x64"
NATIVE_LAUNCHER="$LAUNCHER_DIR/setup-and-run-native"

find_dotnet() {
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

DOTNET="$(find_dotnet)"
ARCH="$(uname -m)"

dotnet_publish() {
    local rid="$1"
    local output="$2"
    "$DOTNET" publish "$SETUP_PROJECT" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$output" \
        --nologo
}

dotnet_publish osx-arm64 "$ARM64_OUT"
dotnet_publish osx-x64 "$X64_OUT"

if command -v lipo >/dev/null 2>&1; then
    lipo -create \
        "$ARM64_OUT/CursorUsageWidget.Setup" \
        "$X64_OUT/CursorUsageWidget.Setup" \
        -output "$NATIVE_LAUNCHER"
else
    case "$ARCH" in
        arm64) cp "$ARM64_OUT/CursorUsageWidget.Setup" "$NATIVE_LAUNCHER" ;;
        x86_64) cp "$X64_OUT/CursorUsageWidget.Setup" "$NATIVE_LAUNCHER" ;;
        *) echo "Unsupported macOS architecture: $ARCH" >&2; exit 1 ;;
    esac
fi

chmod +x "$NATIVE_LAUNCHER"
cp "$NATIVE_LAUNCHER" "$LAUNCHER_DIR/setup-and-run"
rm -rf "$ARM64_OUT" "$X64_OUT"

if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$REPO_ROOT/setup-and-run.app"
fi

if command -v xattr >/dev/null 2>&1; then
    xattr -cr "$REPO_ROOT/setup-and-run.app" || true
fi

echo "$NATIVE_LAUNCHER"
