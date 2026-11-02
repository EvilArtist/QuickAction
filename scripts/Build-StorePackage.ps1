[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9.-]{3,50}$')][string]$IdentityName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [Parameter(Mandatory)][version]$Version
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$storeDisplayName = 'Quick Actions'
if ($Version.Major -lt 1 -or $Version.Revision -ne 0 -or $Version.Minor -lt 0 -or $Version.Build -lt 0 -or
    $Version.Major -gt 65535 -or $Version.Minor -gt 65535 -or $Version.Build -gt 65535) {
    throw 'Store version must be Major.Minor.Build.0 with Major >= 1 and each component <= 65535.'
}
$repo = Split-Path $PSScriptRoot -Parent
$sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$makeAppx = Get-ChildItem $sdkBin -Filter makeappx.exe -Recurse |
    Where-Object { $_.Directory.Name -eq 'x64' } |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (!$makeAppx) { throw 'Install the Windows 10/11 SDK (MakeAppx x64).' }
# A fresh directory prevents deleted/renamed application files leaking into later packages.
$stage = Join-Path $repo ('dist/store-stage/' + [guid]::NewGuid().ToString('N'))
$output = Join-Path $repo 'dist/store'
New-Item -ItemType Directory -Force -Path $stage,$output | Out-Null
dotnet publish (Join-Path $repo 'QuickAction/QuickAction.csproj') -c Release -r win-x64 --self-contained true -o $stage -m:1 -p:UseSharedCompilation=false -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed' }
[xml]$manifest = Get-Content (Join-Path $repo 'packaging/AppxManifest.xml') -Raw
$manifest.Package.Identity.SetAttribute('Name', $IdentityName)
$manifest.Package.Identity.SetAttribute('Publisher', $Publisher)
$manifest.Package.Identity.SetAttribute('Version', $Version.ToString())
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$ns.AddNamespace('appx', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$ns.AddNamespace('uap', 'http://schemas.microsoft.com/appx/manifest/uap/windows10')
$propertiesDisplayNameNode = $manifest.SelectSingleNode('/appx:Package/appx:Properties/appx:DisplayName', $ns)
if (!$propertiesDisplayNameNode) { throw 'Appx manifest is missing Package/Properties/DisplayName.' }
$propertiesDisplayNameNode.InnerText = $storeDisplayName
$visualElementsNode = $manifest.SelectSingleNode('/appx:Package/appx:Applications/appx:Application/uap:VisualElements', $ns)
if (!$visualElementsNode) { throw 'Appx manifest is missing Applications/Application/uap:VisualElements.' }
$visualElementsNode.SetAttribute('DisplayName', $storeDisplayName)
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))

# Generate the app's simple four-button mark at the exact manifest sizes.
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Path $assets | Out-Null
foreach ($entry in @(@('StoreLogo.png',50),@('Square44x44Logo.png',44),@('Square150x150Logo.png',150))) {
    $size = [int]$entry[1]
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $brush = [Drawing.SolidBrush]::new([Drawing.Color]::White)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(37,99,235))
        foreach ($x in @(0.20,0.56)) { foreach ($y in @(0.20,0.56)) {
            $graphics.FillRectangle($brush,[single]($size*$x),[single]($size*$y),[single]($size*0.24),[single]($size*0.24))
        } }
        $bitmap.Save((Join-Path $assets $entry[0]),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$package = Join-Path $output "QuickActions_$($Version)_x64.msix"
# Keep MakeAppx's schema/semantic validation enabled. Store signs the submitted package.
& $makeAppx.FullName pack /d $stage /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation/packaging failed' }
Write-Host "Store package: $package"
