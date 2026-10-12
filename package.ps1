$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$releaseName = 'BongoCatAutoChest-v1.2.0-windows'
$packageFolder = Join-Path $PSScriptRoot "artifacts\$releaseName"
New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null
$files = [ordered]@{
    'BongoCatAutoChest.exe' = 'artifacts\BongoCatAutoChest.exe'
    'BongoCatAutoChest.Cli.exe' = 'artifacts\BongoCatAutoChest.Cli.exe'
    'Mono.Cecil.dll' = 'artifacts\Mono.Cecil.dll'
    'README.md' = 'README.md'
    'LICENSE' = 'LICENSE'
    'THIRD-PARTY-NOTICES.md' = 'THIRD-PARTY-NOTICES.md'
    'docs\game-settings.png' = 'docs\game-settings.png'
}
foreach ($entry in $files.GetEnumerator()) {
    if (!(Test-Path -LiteralPath $entry.Value -PathType Leaf)) { throw "Missing distributable: $($entry.Value). Run build.ps1 first." }
    $destination = Join-Path $packageFolder $entry.Key
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $entry.Value -Destination $destination -Force
}
$expected = @($files.Keys)
$packagePrefix = [IO.Path]::GetFullPath($packageFolder).TrimEnd('\') + '\'
foreach ($file in (Get-ChildItem -LiteralPath $packageFolder -File -Recurse -Force)) {
    $relative = $file.FullName.Substring($packagePrefix.Length)
    if ($relative -notin $expected) { throw "Unexpected package file $relative. Refusing to publish." }
}
foreach ($directory in (Get-ChildItem -LiteralPath $packageFolder -Directory -Recurse -Force)) {
    $relative = $directory.FullName.Substring($packagePrefix.Length)
    if ($relative -ne 'docs' -or ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Unexpected package subdirectory $relative. Refusing to publish." }
}
$archive = Join-Path $PSScriptRoot "artifacts\$releaseName.zip"
Compress-Archive -Path (Join-Path $packageFolder '*') -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archive.sha256" -Value "$hash  $releaseName.zip" -Encoding ascii
Write-Output "$archive`nSHA256 $hash"
