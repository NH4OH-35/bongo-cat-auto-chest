param([switch]$Test)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$packageVersion = '0.11.6'
$packageHash = 'D2A23832AAA948BA9A01ACC42B5726E34C5F995958F1B30D45C0E7C70B3A72D5'
$packageZip = Join-Path $PSScriptRoot "tools\mono.cecil.$packageVersion.zip"
$packageDir = Join-Path $PSScriptRoot "tools\cecil-$packageVersion"
$cecilDll = Join-Path $packageDir 'lib\net40\Mono.Cecil.dll'
New-Item -ItemType Directory -Path tools,artifacts -Force | Out-Null
if (!(Test-Path -LiteralPath $packageZip)) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -UseBasicParsing -Uri "https://api.nuget.org/v3-flatcontainer/mono.cecil/$packageVersion/mono.cecil.$packageVersion.nupkg" -OutFile $packageZip
}
if ((Get-FileHash -LiteralPath $packageZip -Algorithm SHA256).Hash -ne $packageHash) { throw 'Mono.Cecil package hash mismatch. Refusing to build.' }
Expand-Archive -LiteralPath $packageZip -DestinationPath $packageDir -Force
$compiler = Join-Path ([Environment]::GetFolderPath('Windows')) 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4 compiler not found. Build on Windows with .NET Framework 4.8.' }
$commonArgs = @('/nologo', '/optimize+', '/platform:anycpu', "/reference:$cecilDll", '/reference:System.Core.dll', '/reference:System.Runtime.Serialization.dll')
& $compiler /nologo /optimize+ /nowarn:0649 /target:library /out:artifacts\RuntimeTemplate.dll src\RuntimeStubs.cs src\RuntimeFeatures.cs
if ($LASTEXITCODE -ne 0) { throw 'Runtime template compilation failed.' }
$commonArgs += '/resource:artifacts\RuntimeTemplate.dll,BongoClaim.RuntimeTemplate.dll'
Copy-Item -LiteralPath $cecilDll -Destination artifacts\Mono.Cecil.dll -Force
& $compiler @commonArgs /target:winexe /out:artifacts\BongoClaim.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /win32manifest:src\app.manifest src\Program.cs src\PatchEngine.cs src\NativeControlsPatch.cs src\OwnedTokensPatch.cs src\MainForm.cs
if ($LASTEXITCODE -ne 0) { throw 'Desktop compilation failed.' }
& $compiler @commonArgs /target:exe /out:artifacts\BongoClaim.Cli.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll src\Program.cs src\PatchEngine.cs src\NativeControlsPatch.cs src\OwnedTokensPatch.cs src\MainForm.cs
if ($LASTEXITCODE -ne 0) { throw 'CLI compilation failed.' }
if ($Test) {
    & $compiler /nologo /optimize+ /target:library /out:artifacts\Assembly-CSharp.dll tests\GameFixture.cs
    if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
    Copy-Item -LiteralPath $cecilDll -Destination artifacts\Mono.Cecil.dll -Force
    & $compiler @commonArgs /target:exe /out:artifacts\BongoClaim.Tests.exe src\PatchEngine.cs src\NativeControlsPatch.cs src\OwnedTokensPatch.cs tests\EngineTests.cs
    if ($LASTEXITCODE -ne 0) { throw 'Tests compilation failed.' }
    & .\artifacts\BongoClaim.Tests.exe
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Write-Output 'Built artifacts\BongoClaim.exe (portable desktop app) and BongoClaim.Cli.exe.'
