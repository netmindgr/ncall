param(
    [ValidateSet('Win32','x64')][string]$Platform = 'Win32',
    [ValidateSet('Release','Debug')][string]$Configuration = 'Release',
    [switch]$CheckOnly,
    [string]$MSBuildPath
)
$ErrorActionPreference = 'Stop'
$packageRoot = $PSScriptRoot
$missing = [System.Collections.Generic.List[string]]::new()
$msbuildCommand = Get-Command msbuild.exe -ErrorAction SilentlyContinue
if (!$MSBuildPath -and $msbuildCommand) { $MSBuildPath = $msbuildCommand.Source }
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!$MSBuildPath -and (Test-Path -LiteralPath $vswherePath)) {
    $MSBuildPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
}
if (!$MSBuildPath) { $missing.Add('MSBuild / Visual Studio 2022 C++ v143, MFC and Windows SDK 10.0.26100.0.') }
foreach ($dependency in @('pjlib/include/pj/config_site.h','pjsip/include/pjsua-lib/pjsua.h','pjmedia/include/pjmedia.h','pjnath/include/pjnath.h','pjlib-util/include/pjlib-util.h','third_party/netmind/include/openssl/ssl.h','third_party/netmind/include/opus/opus.h','third_party/netmind/lib/libssl.lib','third_party/netmind/lib/libcrypto.lib','third_party/netmind/lib/opus.lib','third_party/netmind/lib/bcg729.lib','third_party/netmind/lib/SQLiteCpp.lib','third_party/netmind/lib/sqlite3.lib')) {
    if (!(Test-Path -LiteralPath (Join-Path $packageRoot $dependency))) { $missing.Add($dependency) }
}
if ($Platform -ne 'Win32' -or $Configuration -ne 'Release') { $missing.Add('This initial package supports Release Win32 only; other configurations need matching dependency binaries.') }
if ($missing.Count) {
    Write-Host 'Build prerequisites are missing:'
    $missing | ForEach-Object { Write-Host " - $_" }
    Write-Host 'This is a preliminary check; further libraries depend on the PJSIP configuration.'
    exit 2
}
if ($CheckOnly) { Write-Host 'Preliminary prerequisites found. Compilation is still required for verification.'; exit 0 }
& $MSBuildPath (Join-Path $packageRoot 'pjsip-apps/build/libpjproject.vcxproj') /m /v:minimal /p:Configuration=Release-Static /p:Platform=Win32 /p:BuildToolset=v143 /p:PlatformVersion=10.0.26100.0 /p:PlatformToolset=v143 /p:WindowsTargetPlatformVersion=10.0.26100.0
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $packageRoot 'source/ui/build.ps1')
& $MSBuildPath (Join-Path $packageRoot 'source/microsip.vcxproj') /m /v:minimal "/p:Configuration=$Configuration" "/p:Platform=$Platform" /p:PlatformToolset=v143 /p:BuildToolset=v143 /p:WindowsTargetPlatformVersion=10.0.26100.0 /p:PlatformVersion=10.0.26100.0
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$binaryRoot = Join-Path $packageRoot "build/$Platform/$Configuration"
Copy-Item -LiteralPath (Join-Path $packageRoot 'langpack_greek.txt') -Destination $binaryRoot
Copy-Item -Path (Join-Path $packageRoot 'source/res/sounds/*') -Destination $binaryRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $packageRoot 'COPYING') -Destination $binaryRoot
Copy-Item -LiteralPath (Join-Path $packageRoot 'NOTICE.txt') -Destination $binaryRoot
Write-Host "Build finished: $binaryRoot/ncall.exe"
