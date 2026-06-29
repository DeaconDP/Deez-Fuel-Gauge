#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LAUNCHER_DIR="$REPO_ROOT/setup-and-run.app/Contents/MacOS"
SETUP_PROJECT="$REPO_ROOT/DeezFuelGauge.Setup/DeezFuelGauge.Setup.csproj"
ARM64_OUT="$LAUNCHER_DIR/publish-arm64"
X64_OUT="$LAUNCHER_DIR/publish-x64"
NATIVE_LAUNCHER="$LAUNCHER_DIR/setup-and-run-native"

# shellcheck source=ensure-dotnet8-sdk.sh
source "$REPO_ROOT/scripts/ensure-dotnet8-sdk.sh"
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
        "$ARM64_OUT/DeezFuelGauge.Setup" \
        "$X64_OUT/DeezFuelGauge.Setup" \
        -output "$NATIVE_LAUNCHER"
else
    case "$ARCH" in
        arm64) cp "$ARM64_OUT/DeezFuelGauge.Setup" "$NATIVE_LAUNCHER" ;;
        x86_64) cp "$X64_OUT/DeezFuelGauge.Setup" "$NATIVE_LAUNCHER" ;;
        *) echo "Unsupported macOS architecture: $ARCH" >&2; exit 1 ;;
    esac
fi

chmod +x "$NATIVE_LAUNCHER"
cp "$NATIVE_LAUNCHER" "$LAUNCHER_DIR/setup-and-run"
mkdir -p "$REPO_ROOT/setup-and-run.app/Contents/Resources"
if [[ -f "$REPO_ROOT/packaging/icons/AppIcon.icns" ]]; then
    cp "$REPO_ROOT/packaging/icons/AppIcon.icns" "$REPO_ROOT/setup-and-run.app/Contents/Resources/AppIcon.icns"
fi
rm -rf "$ARM64_OUT" "$X64_OUT"

if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$REPO_ROOT/setup-and-run.app"
fi

if command -v xattr >/dev/null 2>&1; then
    xattr -cr "$REPO_ROOT/setup-and-run.app" || true
fi

echo "$NATIVE_LAUNCHER"
