param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'Temp/WalletSessionTests'
New-Item -ItemType Directory -Force $output | Out-Null
$runtime = Get-ChildItem "$UnityData/NetCoreRuntime/shared/Microsoft.NETCore.App" -Directory | Sort-Object Name -Descending | Select-Object -First 1
$compileArgs = @('-nologo', '-target:exe', '-define:SIMPLE_MARKET_REOWN', "-out:`"$output/Tests.dll`"")
$compileArgs += Get-ChildItem $runtime.FullName -Filter '*.dll' | ForEach-Object {
    try { $null = [Reflection.AssemblyName]::GetAssemblyName($_.FullName); "-r:`"$($_.FullName)`"" } catch { }
}
$compileArgs += "`"$PSScriptRoot/Program.cs`""
$compileArgs += "`"$projectRoot/Assets/Scripts/SimpleMarket/ReownWalletBridge.cs`""
$rsp = Join-Path $output 'Tests.rsp'
Set-Content $rsp $compileArgs
& "$UnityData/NetCoreRuntime/dotnet.exe" "$UnityData/DotNetSdkRoslyn/csc.dll" "@$rsp"
if ($LASTEXITCODE -ne 0) { throw 'Wallet test compilation failed' }
@{ runtimeOptions = @{ tfm = 'net6.0'; framework = @{ name = 'Microsoft.NETCore.App'; version = $runtime.Name } } } |
    ConvertTo-Json -Depth 4 | Set-Content "$output/Tests.runtimeconfig.json"
& "$UnityData/NetCoreRuntime/dotnet.exe" "$output/Tests.dll"
if ($LASTEXITCODE -ne 0) { throw 'Wallet session tests failed' }
