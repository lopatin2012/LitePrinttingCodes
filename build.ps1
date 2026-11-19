[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$project = Join-Path $root "src\GS1LabelPrinter\GS1LabelPrinter.csproj"
$dist = Join-Path $root "dist"

if (-not (Test-Path $project)) {
    throw "Не найден проект: $project"
}

if (Test-Path $dist) {
    Get-ChildItem -LiteralPath $dist -Force | Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "Сборка GS1LabelPrinter ($Configuration, $Runtime)..." -ForegroundColor Cyan

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $dist

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с ошибкой."
}

$documents = @(
    "README.md",
    "THIRD-PARTY-NOTICES.txt",
    "LICENSE",
    "CHANGELOG.md",
    "CONTRIBUTING.md"
)

foreach ($document in $documents) {
    Copy-Item -LiteralPath (Join-Path $root $document) -Destination $dist -Force
}

$licenses = Join-Path $root "LICENSES"
if (Test-Path $licenses) {
    Copy-Item -LiteralPath $licenses -Destination $dist -Recurse -Force
}

$exe = Join-Path $dist "GS1LabelPrinter.exe"
if (-not (Test-Path $exe)) {
    throw "Не найден результат сборки: $exe"
}

$item = Get-Item -LiteralPath $exe
Write-Host ""
Write-Host "Готово: $($item.FullName)" -ForegroundColor Green
Write-Host ("Размер exe: {0:N1} МБ" -f ($item.Length / 1MB)) -ForegroundColor Green
