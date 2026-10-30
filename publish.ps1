$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$project = [xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BetterStickyNotes.csproj') -Raw)
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version de publication invalide.' }
$dist = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'dist'))
New-Item -ItemType Directory -Force -Path $dist | Out-Null
if ((Get-Item -LiteralPath $dist).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Le dossier dist ne doit pas être un lien.' }
$output = Join-Path $dist "BetterStickyNotes-$version"
$archive = Join-Path $dist "BetterStickyNotes-$version-win-x64.zip"
if ((Test-Path -LiteralPath $output) -and ((Get-Item -LiteralPath $output).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Le dossier de sortie ne doit pas être un lien.' }

dotnet publish BetterStickyNotes.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None --ignore-failed-sources -o $output
if ($LASTEXITCODE -ne 0) { throw 'Échec de la publication.' }
Copy-Item -LiteralPath README.md -Destination (Join-Path $output 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs') -Destination $output -Recurse -Force
Compress-Archive -Path (Join-Path $output '*') -DestinationPath $archive -Force

# Retain at most three distinct versions, counting folders and ZIPs together.
# Cleanup only starts after a successful publish and archive creation.
$artifacts = @(Get-ChildItem -LiteralPath $dist | ForEach-Object {
    if ($_.Name -match '^BetterStickyNotes-v?(\d+\.\d+\.\d+)(-win-x64\.zip)?$' -or $_.Name -match '^release-notes-(\d+\.\d+\.\d+)\.md$') {
        [pscustomobject]@{ Item = $_; Version = [version]$Matches[1] }
    }
})
$keep = @($artifacts.Version | Sort-Object -Unique -Descending | Select-Object -First 3)
$obsolete = @($artifacts | Where-Object { $_.Version -notin $keep } | ForEach-Object { $_.Item })
# Legacy unversioned artifacts predate the versioned distribution layout.
$obsolete += @(Get-ChildItem -LiteralPath $dist | Where-Object { $_.Name -in @('BetterStickyNotes', 'BetterStickyNotes-win-x64.zip', 'release-notes.md') })
foreach ($item in $obsolete) {
    $target = [IO.Path]::GetFullPath($item.FullName)
    if ([IO.Path]::GetDirectoryName($target) -ne $dist) { throw "Suppression hors de dist refusée : $target" }
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Suppression d'un lien refusée : $target" }
    if ($item.PSIsContainer -and (Get-ChildItem -LiteralPath $target -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })) {
        throw "Le dossier contient un lien : $target"
    }
    Write-Host "Ancienne distribution supprimée : $target"
    Remove-Item -LiteralPath $target -Recurse -Force
}
Get-ChildItem -LiteralPath $dist -Filter 'BetterStickyNotes-*-win-x64.zip' | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Prêt à lancer : $(Join-Path $output 'BetterStickyNotes.exe')"
