#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "The .NET 8 SDK is required."
  echo "Install it from https://dotnet.microsoft.com/download/dotnet/8.0"
  echo "or run: brew install --cask dotnet-sdk"
  exit 1
fi

dotnet build CursorUsageWidget.sln -c Release
dotnet run --project CursorUsageWidget -c Release --no-build
