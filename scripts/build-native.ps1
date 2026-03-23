param(
    [string]$Configuration = "Debug"
)

$Root = Split-Path -Parent $PSScriptRoot
$BuildDir = Join-Path $Root "out/native/$Configuration"
$ArtifactDir = Join-Path $Root "artifacts/native/$Configuration"

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null
New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

cmake -S "$Root/native/Rex.Native" -B "$BuildDir" -DCMAKE_BUILD_TYPE=$Configuration -DREX_OUTPUT_DIR="$ArtifactDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

cmake --build "$BuildDir" --config $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

ctest --test-dir "$BuildDir" --output-on-failure
exit $LASTEXITCODE
