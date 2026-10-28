$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$outputPath = Join-Path $projectRoot 'dist/QuickAction-win-x64'
dotnet publish (Join-Path $projectRoot 'QuickAction/QuickAction.csproj') -c Release -r win-x64 --self-contained true -o $outputPath -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputPath
Copy-Item -LiteralPath (Join-Path $projectRoot 'VALIDATION.md') -Destination $outputPath
Compress-Archive -Path (Join-Path $outputPath '*') -DestinationPath (Join-Path $projectRoot 'dist/QuickAction-win-x64.zip') -Force
