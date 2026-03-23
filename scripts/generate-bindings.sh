#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet run --project "$ROOT_DIR/src/Tools/Rex.PInvoke.Tool/Rex.PInvoke.Tool.csproj" -- \
  "$ROOT_DIR/native/Rex.Native/include/rex/rex_native.h" \
  "$ROOT_DIR/src/Shared/Rex.Native/Generated/Methods.g.cs" \
  Rex.Native
