param([string]$OutputDirectory = 'artifacts/USB-Atlas-win-x64')
$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
    dotnet test tests/UsbAtlas.Tests/UsbAtlas.Tests.csproj -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    dotnet publish src/UsbAtlas.UI/UsbAtlas.UI.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory "-p:PathMap=$PSScriptRoot=/_/" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory 'UsbAtlas.UI.pri'))) { throw 'XAML resource index is missing.' }
    Copy-Item -LiteralPath README.md -Destination (Join-Path $OutputDirectory 'README.md')\n    Copy-Item -LiteralPath LICENSE -Destination (Join-Path $OutputDirectory 'LICENSE')
    Copy-Item -LiteralPath docs/IMPLEMENTATION_STATUS.md -Destination (Join-Path $OutputDirectory 'IMPLEMENTATION_STATUS.md')
    Copy-Item -LiteralPath docs/VALIDATION.md -Destination (Join-Path $OutputDirectory 'VALIDATION.md')
    Copy-Item -LiteralPath docs/RESEARCH_V4_1.md -Destination (Join-Path $OutputDirectory 'RESEARCH_V4_1.md')
    Copy-Item -LiteralPath THIRD_PARTY_NOTICES.md -Destination (Join-Path $OutputDirectory 'THIRD_PARTY_NOTICES.md')
    Copy-Item -LiteralPath docs/licenses -Destination $OutputDirectory -Recurse -Force
    $zipPath = $OutputDirectory.TrimEnd('/', '\') + '.zip'
    Compress-Archive -LiteralPath $OutputDirectory -DestinationPath $zipPath -Force
    Get-FileHash -LiteralPath $zipPath -Algorithm SHA256 | Format-List
}
finally { Pop-Location }
