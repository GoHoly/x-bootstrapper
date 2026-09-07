$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnet = Join-Path $env:LOCALAPPDATA "dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

$publishDir = Join-Path $root "publish\app"
$distDir = Join-Path $root "dist"
$iss = Join-Path $root "installer\XBootstrapper.iss"
$csproj = Join-Path $root "src\Caelus\Caelus.csproj"

Write-Host "Publishing X Bootstrapper (self-contained win-x64)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $publishDir, $distDir | Out-Null

& $dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

function Get-Iscc {
    $existing = Get-ChildItem (Join-Path $root "tools") -Recurse -Filter ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($existing) { return $existing.FullName }
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    return $null
}

$iscc = Get-Iscc
if (-not $iscc) {
    Write-Host "Downloading Inno Setup compiler..."
    $tools = Join-Path $root "tools"
    $nupkg = Join-Path $tools "innosetup.nupkg"
    $extract = Join-Path $tools "inno-nuget"
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    Invoke-WebRequest -UseBasicParsing -Uri "https://globalcdn.nuget.org/packages/tools.innosetup.6.4.3.nupkg" -OutFile $nupkg
    $zip = Join-Path $tools "innosetup.zip"
    Copy-Item -LiteralPath $nupkg -Destination $zip -Force
    if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $extract -Force
    $found = Get-ChildItem $extract -Recurse -Filter ISCC.exe | Select-Object -First 1
    if (-not $found) { throw "ISCC.exe was not in the Inno Setup package." }
    $iscc = $found.FullName
}

Write-Host "Building installer with $iscc"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

$setup = Join-Path $distDir "X Bootstrapper Setup.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw "Setup.exe was not created." }
Write-Host "Installer: $setup"
Get-Item -LiteralPath $setup | Select-Object FullName, Length, LastWriteTime
