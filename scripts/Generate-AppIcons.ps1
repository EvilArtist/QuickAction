$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repo = Split-Path $PSScriptRoot -Parent
$iconDir = Join-Path $repo 'QuickAction/Icon'
$assets = Join-Path $repo 'packaging/Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$source = [Drawing.Image]::FromFile((Join-Path $iconDir 'QuickAction.png'))
function Get-IconPng([int]$size) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $stream = [IO.MemoryStream]::new()
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source,0,0,$size,$size)
        $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        return ,$stream.ToArray()
    } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
try {
    $sizes = @(16,20,24,32,40,48,64,96,128,256)
    $frames = @($sizes | ForEach-Object { ,(Get-IconPng $_) })
    $stream = [IO.File]::Create((Join-Path $iconDir 'QuickAction.ico'))
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $stream.Dispose() }
    foreach ($entry in @(@('StoreLogo',50),@('Square44x44Logo',44),@('Square150x150Logo',150))) {
        [IO.File]::WriteAllBytes((Join-Path $assets ($entry[0]+'.png')),(Get-IconPng $entry[1]))
        foreach ($scale in @(100,200,400)) {
            [IO.File]::WriteAllBytes((Join-Path $assets ($entry[0]+".scale-$scale.png")),(Get-IconPng ($entry[1]*$scale/100)))
        }
    }
    foreach ($size in @(16,24,32,48,256)) {
        foreach ($suffix in @('', '_altform-unplated')) {
            [IO.File]::WriteAllBytes((Join-Path $assets "Square44x44Logo.targetsize-$size$suffix.png"),(Get-IconPng $size))
        }
    }
} finally { $source.Dispose() }
