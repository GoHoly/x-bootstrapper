$dotnet = Join-Path $env:LOCALAPPDATA "dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
& $dotnet build "$PSScriptRoot\src\Caelus\Caelus.csproj" -c Release
Write-Host "Output: $PSScriptRoot\src\Caelus\bin\Release\net8.0-windows10.0.17763.0\X Bootstrapper.exe"
