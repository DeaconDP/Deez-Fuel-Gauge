#!/bin/bash

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

# shellcheck source=ensure-dotnet8-sdk.sh
source "$REPO_ROOT/scripts/ensure-dotnet8-sdk.sh"

exec "$DOTNET" run --project "$REPO_ROOT/DeezFuelGauge.Setup/DeezFuelGauge.Setup.csproj" -c Release --nologo
