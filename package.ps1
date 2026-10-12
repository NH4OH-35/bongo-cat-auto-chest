$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$releaseName = 'BongoClaim-v1.1.0-windows'
$packageFolder = Join-Path $PSScriptRoot "artifacts\$releaseName"
New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null
$files = @('artifacts\BongoClaim.exe','artifacts\BongoClaim.Cli.exe','artifacts\Mono.Cecil.dll','README.md','LICENSE','THIRD-PARTY-NOTICES.md')
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath $file)) { throw "Missing distributable: $file. Run build.ps1 first." }
    Copy-Item -LiteralPath $file -Destination $packageFolder -Force
}
$expected = @('BongoClaim.exe','BongoClaim.Cli.exe','Mono.Cecil.dll','README.md','LICENSE','THIRD-PARTY-NOTICES.md')
foreach ($file in (Get-ChildItem -LiteralPath $packageFolder -File)) {
    if ($file.Name -notin $expected) { throw "Unexpected package file $($file.Name). Refusing to publish." }
}
if (Get-ChildItem -LiteralPath $packageFolder -Directory) { throw 'Unexpected package subdirectory. Refusing to publish.' }
$archive = Join-Path $PSScriptRoot "artifacts\$releaseName.zip"
Compress-Archive -LiteralPath ($expected | ForEach-Object { Join-Path $packageFolder $_ }) -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archive.sha256" -Value "$hash  $releaseName.zip" -Encoding ascii
Write-Output "$archive`nSHA256 $hash"
