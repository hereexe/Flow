param (
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building Flow Release v$Version" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$RootDir = $PSScriptRoot
$PublishDir = Join-Path $RootDir "publish\win-x64"
$DistDir = Join-Path $RootDir "dist"

# 1. Clean output directories
Write-Host "`n[1/4] Cleaning previous output directories..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }

# 2. Dotnet publish (Self-contained, Single-File win-x64)
Write-Host "`n[2/4] Publishing Flow.Presentation (win-x64 self-contained)..." -ForegroundColor Yellow
$publishArgs = @(
    "publish",
    "Flow.Presentation\Flow.Presentation.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-o", $PublishDir
)
& dotnet $publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}

# 3. Create Portable ZIP
Write-Host "`n[3/4] Creating Portable ZIP archive..." -ForegroundColor Yellow
$ZipPath = Join-Path $DistDir "Flow-Portable-v$Version-win-x64.zip"
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Created: $ZipPath" -ForegroundColor Green

# 4. Compile Inno Setup Installer (if ISCC is available)
Write-Host "`n[4/4] Checking for Inno Setup compiler (ISCC)..." -ForegroundColor Yellow
$isccPaths = @(
    "ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

$isccExe = $null
foreach ($path in $isccPaths) {
    if (Get-Command $path -ErrorAction SilentlyContinue) {
        $isccExe = $path
        break
    } elseif (Test-Path $path) {
        $isccExe = $path
        break
    }
}

if ($isccExe) {
    Write-Host "Compiling Inno Setup installer using $isccExe..." -ForegroundColor Yellow
    $issFile = Join-Path $RootDir "installer\flow_setup.iss"
    & $isccExe "/DMyAppVersion=$Version" $issFile
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Created installer in $DistDir\Flow-Setup.exe" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation failed with code $LASTEXITCODE"
    }
} else {
    Write-Host "Inno Setup (ISCC.exe) not found locally. To build Flow-Setup.exe locally, install Inno Setup 6." -ForegroundColor Gray
    Write-Host "(Note: GitHub Actions will build Flow-Setup.exe automatically on release)." -ForegroundColor Gray
}

Write-Host "`n==========================================" -ForegroundColor Cyan
Write-Host " Build Finished Successfully! Artifacts in /dist:" -ForegroundColor Green
Get-ChildItem $DistDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
Write-Host "==========================================" -ForegroundColor Cyan
