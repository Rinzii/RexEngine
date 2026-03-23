#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Debug}"
BUILD_DIR="$ROOT/out/native/$CONFIG"
ARTIFACT_DIR="$ROOT/artifacts/native/$CONFIG"

mkdir -p "$BUILD_DIR" "$ARTIFACT_DIR"
cmake -S "$ROOT/native/Rex.Native" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE="$CONFIG" -DREX_OUTPUT_DIR="$ARTIFACT_DIR"
cmake --build "$BUILD_DIR" --config "$CONFIG"
ctest --test-dir "$BUILD_DIR" --output-on-failure
