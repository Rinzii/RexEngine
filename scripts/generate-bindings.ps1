$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot

dotnet run --project "$Root/src/Tools/Rex.PInvoke.Tool/Rex.PInvoke.Tool.csproj" -- `
  "$Root/native/Rex.Native/include/rex/rex_native.h" `
  "$Root/src/Shared/Rex.Native/Generated/Methods.g.cs" `
  Rex.Native
