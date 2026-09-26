param(
    [string]$UnrealRoot = 'C:\Program Files\Epic Games\UE_5.8',
    [string]$OutputDir = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$projectDir = Join-Path $repoRoot 'unreal\Bigimong'
$project = Join-Path $projectDir 'Bigimong.uproject'
$uat = Join-Path $UnrealRoot 'Engine\Build\BatchFiles\RunUAT.bat'
if (-not $OutputDir) { $OutputDir = Join-Path $repoRoot 'build\Android' }
if (-not (Test-Path -LiteralPath $project -PathType Leaf)) { throw "Missing Unreal project: $project" }
if (-not (Test-Path -LiteralPath $uat -PathType Leaf)) { throw "Unreal 5.8 is required: $uat" }

$maleAssets = Join-Path $projectDir 'Content\Varco\Male'
$required = @('SM_MaleBody', 'SK_MaleFace')
foreach ($kind in @('Eye', 'Hair')) {
    for ($index = 0; $index -lt 15; $index++) {
        $required += ('SM_Male{0}_{1:D2}' -f $kind, $index)
    }
}
foreach ($name in $required) {
    $asset = Join-Path $maleAssets "$name.uasset"
    if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) {
        throw "Unreal Editor import is required: $asset"
    }
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$archive = Join-Path $outputPath ('.unreal-archive-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $archive | Out-Null
try {
    & $uat BuildCookRun "-project=$project" -noP4 -target=Bigimong -platform=Android `
        -clientconfig=Development -build -cook -stage -pak -package -archive `
        "-archivedirectory=$archive" -unattended -utf8output
    if ($LASTEXITCODE -ne 0) { throw "Unreal Android packaging failed with exit code $LASTEXITCODE" }

    $apks = @(Get-ChildItem -LiteralPath $archive -Recurse -File -Filter '*.apk')
    if ($apks.Count -ne 1) { throw "Expected one Unreal Android APK, found $($apks.Count)" }
    $validation = @'
import sys, zipfile
with zipfile.ZipFile(sys.argv[1]) as apk:
    names = apk.namelist()
    if 'AndroidManifest.xml' not in names:
        raise SystemExit('AndroidManifest.xml missing')
    if not any(n.startswith('lib/arm64-v8a/') and n.endswith('.so') for n in names):
        raise SystemExit('arm64 native library missing')
    corrupt = apk.testzip()
    if corrupt:
        raise SystemExit('Corrupt APK member: ' + corrupt)
'@
    & python -c $validation $apks[0].FullName
    if ($LASTEXITCODE -ne 0) { throw "Unreal APK validation failed" }

    $final = Join-Path $outputPath 'Bigimong-Unreal-MalePreview-debug.apk'
    Copy-Item -LiteralPath $apks[0].FullName -Destination $final -Force
    $hash = (Get-FileHash -LiteralPath $final -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$final.sha256" -Value "$hash  $(Split-Path $final -Leaf)" -Encoding ascii
    Write-Output "Built and inspected: $final"
}
finally {
    $archivePath = [System.IO.Path]::GetFullPath($archive)
    $prefix = $outputPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($archivePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -and
            (Test-Path -LiteralPath $archivePath)) {
        Remove-Item -LiteralPath $archivePath -Recurse -Force
    }
}
