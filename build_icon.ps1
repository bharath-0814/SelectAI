Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\bhara\.gemini\antigravity\brain\390bb203-0ade-451f-8177-f6a9da6d20af\selectai_app_logo_1790862556794.jpg"
$assetsDir = "d:\AI  select\SelectAI\Assets"
if (-not (Test-Path $assetsDir)) { 
    New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null 
}

$srcBmp = [System.Drawing.Image]::FromFile($srcPath)

# 1. Save 512x512 master PNG
$pngPath = Join-Path $assetsDir "app_icon.png"
$dest512 = New-Object System.Drawing.Bitmap 512, 512
$g512 = [System.Drawing.Graphics]::FromImage($dest512)
$g512.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g512.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g512.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g512.DrawImage($srcBmp, 0, 0, 512, 512)
$g512.Dispose()
$dest512.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Copy-Item $pngPath "d:\AI  select\app_icon.png" -Force

# 2. Multi-resolution ICO
$sizes = @(256, 128, 64, 48, 32, 16)
$pngData = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($srcBmp, 0, 0, $sz, $sz)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $pngData += [PSCustomObject]@{
        Size = $sz
        Bytes = $bytes
    }
    $ms.Dispose()
    $bmp.Dispose()
}

$srcBmp.Dispose()
$dest512.Dispose()

# 3. Write ICO binary
$icoPath = Join-Path $assetsDir "app_icon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs

$bw.Write([uint16]0)              # Reserved
$bw.Write([uint16]1)              # Type: 1 = ICO
$bw.Write([uint16]$sizes.Count)   # Image Count

$offset = 6 + (16 * $sizes.Count)

foreach ($item in $pngData) {
    $sz = $item.Size
    $bytes = $item.Bytes
    
    $bw.Write([byte]($sz % 256))       # Width (0 for 256)
    $bw.Write([byte]($sz % 256))       # Height (0 for 256)
    $bw.Write([byte]0)                 # Color count
    $bw.Write([byte]0)                 # Reserved
    $bw.Write([uint16]1)               # Color planes
    $bw.Write([uint16]32)              # Bits per pixel
    $bw.Write([uint32]$bytes.Length)   # Image byte length
    $bw.Write([uint32]$offset)         # Image offset
    $offset += $bytes.Length
}

foreach ($item in $pngData) {
    $bw.Write($item.Bytes)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Copy-Item $icoPath "d:\AI  select\favicon.ico" -Force
Write-Host "Created app_icon.ico successfully. Size: $((Get-Item $icoPath).Length) bytes"
