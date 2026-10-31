<#
  生成 DevEverything 的创意工坊封面 Workshop\preview.png（512x512）。

  用法：
      powershell -NoProfile -ExecutionPolicy Bypass -File Tools\MakeWorkshopPreview.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File Tools\MakeWorkshopPreview.ps1 -Title "别的标题"

  版式：深色渐变底 → 金色标题 → 分隔线 → 带边框和投影的原始截图（整数倍放大，保持像素感）

  素材：Workshop\art\cover.png（用户提供的箱子图）
  只依赖 System.Drawing，不改动任何游戏文件。
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $false)][string]$ModDir = '',
	[Parameter(Mandatory = $false)][string]$Title = '中文调试按钮',
	[Parameter(Mandatory = $false)][string]$Art = '',
	[Parameter(Mandatory = $false)][string]$OutFile,
	[Parameter(Mandatory = $false)][int]$Size = 512
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (-not $ModDir) { $ModDir = Split-Path -Parent $PSScriptRoot }
if (-not $ModDir) { $ModDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }
if (-not $Art) { $Art = Join-Path $ModDir 'Workshop\art\cover.png' }
if (-not $OutFile) { $OutFile = Join-Path $ModDir 'Workshop\preview.png' }

$OutDir = Split-Path -Parent $OutFile
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
if (-not (Test-Path $Art)) { throw "找不到封面素材：$Art" }

$bmp = New-Object System.Drawing.Bitmap $Size, $Size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# ------------------------------------------------------------------ 背景
$bgRect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(255, 44, 28, 30)), ([System.Drawing.Color]::FromArgb(255, 10, 7, 8)), 90.0
$g.FillRectangle($bgBrush, $bgRect)
$bgBrush.Dispose()

$bandRect = New-Object System.Drawing.Rectangle 0, 0, $Size, 140
$bandBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bandRect, ([System.Drawing.Color]::FromArgb(200, 104, 26, 30)), ([System.Drawing.Color]::FromArgb(0, 104, 26, 30)), 90.0
$g.FillRectangle($bandBrush, $bandRect)
$bandBrush.Dispose()

# ------------------------------------------------------------------ 标题
$titleFont = New-Object System.Drawing.Font 'Microsoft YaHei', 44, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 0, 0, 0))
$titleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 244, 219, 168))
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

$g.DrawString($Title, $titleFont, $shadowBrush, (New-Object System.Drawing.RectangleF 3, 29, $Size, 58), $fmt)
$g.DrawString($Title, $titleFont, $titleBrush, (New-Object System.Drawing.RectangleF 0, 26, $Size, 58), $fmt)

$linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(130, 190, 146, 90)), 2
$g.DrawLine($linePen, 90, 104, $Size - 90, 104)
$linePen.Dispose()

# ------------------------------------------------------------------ 截图：整数倍放大 + 投影 + 边框
$img = [System.Drawing.Image]::FromFile($Art)
$scale = 1
while (($img.Width * ($scale + 1) -le $Size - 56) -and ($img.Height * ($scale + 1) -le $Size - 150)) { $scale++ }

$w = $img.Width * $scale
$h = $img.Height * $scale
$ix = [int](($Size - $w) / 2)
$iy = 128 + [int](($Size - 128 - 20 - $h) / 2)
if ($iy -lt 128) { $iy = 128 }

$shadowBrush2 = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 0, 0, 0))
$g.FillRectangle($shadowBrush2, ($ix + 6), ($iy + 8), $w, $h)
$shadowBrush2.Dispose()

$g.DrawImage($img, (New-Object System.Drawing.Rectangle $ix, $iy, $w, $h))
$img.Dispose()

$framePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 190, 146, 90)), 2
$g.DrawRectangle($framePen, ($ix - 3), ($iy - 3), ($w + 5), ($h + 5))
$framePen.Dispose()

# ------------------------------------------------------------------ 保存
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/png' }
$params = New-Object System.Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
$bmp.Save($OutFile, $enc, $params)

$titleFont.Dispose(); $shadowBrush.Dispose(); $titleBrush.Dispose(); $fmt.Dispose()
$g.Dispose(); $bmp.Dispose()

$info = Get-Item $OutFile
Write-Host ("已生成 {0}（{1} 字节，素材放大 {2} 倍）" -f $info.FullName, $info.Length, $scale)
