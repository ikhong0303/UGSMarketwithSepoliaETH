$ErrorActionPreference = 'Stop'
dotnet publish "$PSScriptRoot/MarketNotifications.csproj" -c Release -r linux-x64 --self-contained false -o "$PSScriptRoot/bin/package"
if ($LASTEXITCODE -ne 0) { throw 'Module publish failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$moduleArchive = Join-Path $PSScriptRoot 'MarketNotifications.ccm'
# Replace only the generated archive in this module directory.
if (Test-Path -LiteralPath $moduleArchive) { Remove-Item -LiteralPath $moduleArchive }
[System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $PSScriptRoot 'bin/package'), $moduleArchive)
Write-Output $moduleArchive
