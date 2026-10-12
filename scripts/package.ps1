param([Parameter(Mandatory=$true)][string]$VcpkgRoot,[string]$ISCCPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
# Version and vcpkg baseline come from vcpkg.json only.
$manifest=Get-Content -LiteralPath (Join-Path $projectRoot 'vcpkg.json') -Raw | ConvertFrom-Json
$version=$manifest.'version-string'
$baseline=$manifest.'builtin-baseline'
$revision=& git -C $VcpkgRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $revision.Trim() -ne $baseline) { throw "Pinned vcpkg checkout required ($baseline)." }
# The source archive is built from the committed tree, so local files never leak into it.
$dirty=& git -C $projectRoot status --porcelain
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
if ($dirty) { throw "Commit or stash local changes before packaging:`n$($dirty -join "`n")" }
$commit=(& git -C $projectRoot rev-parse HEAD).Trim()
$binaryRoot=Join-Path $projectRoot 'build/Win32/Release'
foreach ($relative in @('ncall.exe','netmind.ui.dll','fonts/OpenSans-400.ttf')) {
    if (!(Test-Path -LiteralPath (Join-Path $binaryRoot $relative))) { throw "Build first: $relative missing" }
}
$dependencySources=Join-Path $projectRoot 'third_party/netmind-sources'
if (Test-Path -LiteralPath $dependencySources) { Remove-Item -LiteralPath $dependencySources -Recurse -Force }
New-Item -ItemType Directory -Path $dependencySources -Force | Out-Null
foreach ($name in @('openssl','opus','bcg729','sqlite3','sqlitecpp')) {
    # Recipe: nCall overlay port first (vcpkg-ports), otherwise the pinned vcpkg port.
    $recipe=Join-Path $projectRoot "vcpkg-ports/$name"
    if (!(Test-Path -LiteralPath $recipe)) { $recipe=Join-Path $VcpkgRoot "ports/$name" }
    $port=Get-Content -LiteralPath (Join-Path $recipe 'vcpkg.json') -Raw | ConvertFrom-Json
    $portVersion=@($port.version,$port.'version-string',$port.'version-semver',$port.'version-date') | Where-Object { $_ } | Select-Object -First 1
    $sources=@(Get-ChildItem -LiteralPath (Join-Path $VcpkgRoot "buildtrees/$name/src") -Directory -Filter '*.clean')
    # Several versions may have been built: keep the tree of the version actually used.
    if ($sources.Count -gt 1) { $sources=@($sources | Where-Object { $_.Name -like "*$portVersion*" }) }
    if ($sources.Count -ne 1) { throw "Expected one source tree for $name $portVersion (vcpkg binary cache restores skip buildtrees; rebuild with VCPKG_BINARY_SOURCES=clear)" }
    $target=Join-Path $dependencySources $name
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item -Path (Join-Path $sources[0].FullName '*') -Destination $target -Recurse -Force
    $recipeTarget=Join-Path $dependencySources "$name-recipe"
    New-Item -ItemType Directory -Path $recipeTarget -Force | Out-Null
    Copy-Item -Path (Join-Path $recipe '*') -Destination $recipeTarget -Recurse -Force
}
foreach ($name in @('vcpkg-cmake','vcpkg-cmake-config','vcpkg-cmake-get-vars')) {
    Copy-Item -LiteralPath (Join-Path $VcpkgRoot "ports/$name") -Destination $dependencySources -Recurse -Force
}
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$prefix="ncall-$version"
$sourceArchive=Join-Path $projectRoot "ncall-$version-source.zip"
if (Test-Path -LiteralPath $sourceArchive) { Remove-Item -LiteralPath $sourceArchive }
& git -C $projectRoot archive --format=zip "--prefix=$prefix/" -o $sourceArchive HEAD
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
$zip=[IO.Compression.ZipFile]::Open($sourceArchive,[IO.Compression.ZipArchiveMode]::Update)
try {
    foreach ($item in Get-ChildItem -LiteralPath $dependencySources -Recurse -File -Force) {
        $relative=$item.FullName.Substring($projectRoot.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$item.FullName,"$prefix/$relative",[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $entry=$zip.CreateEntry("$prefix/SOURCE-COMMIT.txt")
    $writer=New-Object IO.StreamWriter($entry.Open())
    try { $writer.WriteLine("nCall $version"); $writer.WriteLine("commit $commit") } finally { $writer.Dispose() }
} finally { $zip.Dispose() }
$stage=Join-Path $projectRoot ('release-ncall/stage-'+[Guid]::NewGuid().ToString('N'))
$payload=Join-Path $stage 'ncall'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
foreach ($name in @('ncall.exe','netmind.ui.dll')) { Copy-Item -LiteralPath (Join-Path $binaryRoot $name) -Destination $payload }
Copy-Item -LiteralPath (Join-Path $binaryRoot 'fonts') -Destination $payload -Recurse
Copy-Item -Path (Join-Path $projectRoot 'source/res/sounds/*') -Destination $payload -Recurse
foreach ($name in @('langpack_greek.txt','COPYING','COPYING-GPL-3','NOTICE.txt','README.md','dependency-versions.txt')) { Copy-Item -LiteralPath (Join-Path $projectRoot $name) -Destination $payload }
Copy-Item -LiteralPath (Join-Path $projectRoot 'third_party/netmind/licenses') -Destination $payload -Recurse
# The source archive is published next to the binaries on the GitHub release (see NOTICE.txt).
$portableArchive=Join-Path $projectRoot "ncall-$version-portable.zip"
if (Test-Path -LiteralPath $portableArchive) { Remove-Item -LiteralPath $portableArchive }
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$portableArchive,[IO.Compression.CompressionLevel]::Optimal,$false)
Remove-Item -LiteralPath $stage -Recurse -Force
$outputs=@($sourceArchive,$portableArchive)
if ($ISCCPath) {
    & $ISCCPath /Qp "/DAppVersion=$version" (Join-Path $projectRoot 'installer.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $outputs+=Join-Path $projectRoot "installer/ncall-$version-setup.exe"
    # Same installer under a fixed name: .../releases/latest/download/ncall-setup.exe always gives the latest version.
    $stable=Join-Path $projectRoot 'installer/ncall-setup.exe'
    Copy-Item -LiteralPath (Join-Path $projectRoot "installer/ncall-$version-setup.exe") -Destination $stable -Force
    $outputs+=$stable
}
# Checksums last, so they cover the final (later: signed) files.
$sums=Join-Path $projectRoot "ncall-$version-SHA256SUMS.txt"
$outputs | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(),(Split-Path -Leaf $_) } | Set-Content -LiteralPath $sums -Encoding ascii
# Update feed for nCall's automatic updates: upload latest.json with the release assets.
if ($ISCCPath) {
    $setup = Join-Path $projectRoot "installer/ncall-$version-setup.exe"
    $feed = [ordered]@{
        version = $version
        url = "https://github.com/netmindgr/ncall/releases/download/v$version/ncall-$version-setup.exe"
        sha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
        notes = "Δείτε τις αλλαγές στη σελίδα της έκδοσης."
    }
    $latest = Join-Path $projectRoot 'latest.json'
    [IO.File]::WriteAllText($latest, ($feed | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
    $outputs += $latest
}
Write-Host "Packages created ($commit):"
$outputs + $sums | ForEach-Object { Write-Host " - $_" }
