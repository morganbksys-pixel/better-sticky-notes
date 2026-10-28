$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet publish BetterStickyNotes.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o dist/BetterStickyNotes
if ($LASTEXITCODE -ne 0) { throw 'Échec de la publication.' }
Copy-Item -LiteralPath README.md -Destination dist/BetterStickyNotes/README.md
Compress-Archive -Path dist/BetterStickyNotes/* -DestinationPath dist/BetterStickyNotes-win-x64.zip -Force
