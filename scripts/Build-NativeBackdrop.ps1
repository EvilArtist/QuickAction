param([string]$OutputDirectory)
# Keep this script compatible with Windows PowerShell 5.1: Visual Studio does
# not inherit the optional PowerShell 7 installation used by developer tools.
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot '../QuickAction.Native/Backdrop.cpp'
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$dll = Join-Path $output 'QuickAction.Native.dll'
if ((Test-Path $dll) -and (Get-Item $dll).LastWriteTimeUtc -gt (Get-Item $source).LastWriteTimeUtc -and (Get-Item $dll).LastWriteTimeUtc -gt (Get-Item $PSCommandPath).LastWriteTimeUtc) { exit 0 }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$installation) { throw 'Install Visual Studio C++ desktop build tools and Windows SDK to build the native backdrop.' }
$vcvars = Join-Path $installation 'VC/Auxiliary/Build/vcvars64.bat'
$command = '"{0}" >nul && cl.exe /nologo /LD /MT /EHsc /std:c++20 /O2 /Fo"{1}/Backdrop.obj" /Fe"{2}" "{3}" /link windowsapp.lib dwmapi.lib CoreMessaging.lib user32.lib' -f $vcvars,$output,$dll,$source
& cmd.exe /d /s /c $command
if ($LASTEXITCODE -ne 0) { throw 'Native backdrop build failed.' }



