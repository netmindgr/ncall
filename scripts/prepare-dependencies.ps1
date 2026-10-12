param([string]$VcpkgRoot,[string]$PreparedInstallRoot)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$baseline='e456309491fd875487bf02b8a0dca76c1b1b00cc'
if (!$PreparedInstallRoot) {
    if (!$VcpkgRoot) {
        $VcpkgRoot=Join-Path $projectRoot '.dependencies/vcpkg'
        if (!(Test-Path -LiteralPath $VcpkgRoot)) {
            New-Item -ItemType Directory -Path $VcpkgRoot -Force | Out-Null
            & git -C $VcpkgRoot init
            if ($LASTEXITCODE -ne 0) { throw 'Cannot initialize dependency checkout.' }
            & git -C $VcpkgRoot remote add origin https://github.com/microsoft/vcpkg.git
            & git -C $VcpkgRoot fetch --depth 1 origin $baseline
            if ($LASTEXITCODE -ne 0) { throw 'Cannot fetch pinned vcpkg revision.' }
            & git -C $VcpkgRoot checkout --detach FETCH_HEAD
            if ($LASTEXITCODE -ne 0) { throw 'Cannot select pinned vcpkg revision.' }
        }
    }
    $revision=& git -C $VcpkgRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0 -or $revision.Trim() -ne $baseline) { throw 'Use the vcpkg revision recorded in vcpkg.json.' }
    $vcpkgExe=Join-Path $VcpkgRoot 'vcpkg.exe'
    if (!(Test-Path -LiteralPath $vcpkgExe)) {
        & (Join-Path $VcpkgRoot 'bootstrap-vcpkg.bat') -disableMetrics
        if ($LASTEXITCODE -ne 0) { throw 'vcpkg bootstrap failed.' }
    }
    $installRoot=Join-Path $projectRoot '.dependencies/installed'
    # vcpkg-ports: OpenSSL 3.5 LTS (vcpkg itself stops at 3.5.4).
    & $vcpkgExe install --triplet x86-windows-static "--x-manifest-root=$projectRoot" "--x-install-root=$installRoot" "--overlay-ports=$(Join-Path $projectRoot 'vcpkg-ports')" --disable-metrics
    if ($LASTEXITCODE -ne 0) { throw 'Dependency build failed.' }
    $PreparedInstallRoot=Join-Path $installRoot 'x86-windows-static'
}
foreach ($relative in @('include/openssl/ssl.h','include/opus/opus.h','include/bcg729/decoder.h','lib/libssl.lib','lib/libcrypto.lib','lib/opus.lib','lib/bcg729.lib','lib/SQLiteCpp.lib','lib/sqlite3.lib')) {
    if (!(Test-Path -LiteralPath (Join-Path $PreparedInstallRoot $relative))) { throw "Missing dependency: $relative" }
}
$destination=Join-Path $projectRoot 'third_party/netmind'
New-Item -ItemType Directory -Path (Join-Path $destination 'include'),(Join-Path $destination 'lib'),(Join-Path $destination 'licenses') -Force | Out-Null
Copy-Item -Path (Join-Path $PreparedInstallRoot 'include/*') -Destination (Join-Path $destination 'include') -Recurse -Force
Copy-Item -Path (Join-Path $PreparedInstallRoot 'lib/*.lib') -Destination (Join-Path $destination 'lib') -Force
foreach ($name in @('openssl','opus','bcg729','sqlite3','sqlitecpp')) {
    Copy-Item -LiteralPath (Join-Path $PreparedInstallRoot "share/$name/copyright") -Destination (Join-Path $destination "licenses/$name.txt") -Force
}
Write-Host 'Static x86 dependencies prepared.'
