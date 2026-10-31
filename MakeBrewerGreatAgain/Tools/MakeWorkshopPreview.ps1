<#
  生成创意工坊封面图 Workshop\preview.png （512x512，Steam 推荐尺寸）。

  用法：
      powershell -NoProfile -ExecutionPolicy Bypass -File Tools\MakeWorkshopPreview.ps1

  版式（自上而下）：
      标题 / 副标题 / 分隔线
      营养果 · 营养果块 · 营养果碎        （三颗果子一排，各放大 3 倍）
      啤酒瓶 啤酒瓶 [名片] 啤酒瓶 啤酒瓶   （名片放大 5 倍，正对画面中心）
      恒温发酵机 + 啤酒花                  （底部收尾）

  素材来源：
      营养果          Workshop\art\nutrifruit.png（用 DevEverything 的素材导出从游戏里导出的原版图）
      营养果块/碎      Textures\Items\cutten_nutrifruit.png / mashed_nutrifruit.png
      名片            Textures\icons\card.png（114514 那张）
      啤酒瓶 / 机器 / 啤酒花  Textures 下各自的文件

  只依赖 System.Drawing，不改动任何游戏文件。
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $false)][string]$ModDir = '',
	[Parameter(Mandatory = $false)][string]$OutFile,
	[Parameter(Mandatory = $false)][int]$Size = 512
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# $PSScriptRoot 在 param 默认值里可能是空的，放到这里再算
if (-not $ModDir) { $ModDir = Split-Path -Parent $PSScriptRoot }
if (-not $ModDir) { $ModDir = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }

if (-not $OutFile) { $OutFile = Join-Path $ModDir 'Workshop\preview.png' }
$OutDir = Split-Path -Parent $OutFile
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$texDir = Join-Path $ModDir 'Textures'
$artDir = Join-Path $ModDir 'Workshop\art'

function Get-Sprite([string]$name) {
	foreach ($root in @($artDir, $texDir)) {
		if (-not (Test-Path $root)) { continue }
		$p = Join-Path $root $name
		if (Test-Path $p) { return [System.Drawing.Image]::FromFile($p) }
		# 贴图分在 Textures\Items、Textures\icons 等子目录里，按文件名递归找
		$hit = Get-ChildItem $root -Recurse -File -Filter $name -ErrorAction SilentlyContinue | Select-Object -First 1
		if ($hit) { return [System.Drawing.Image]::FromFile($hit.FullName) }
	}
	Write-Host "缺少贴图，跳过：$name"
	return $null
}

$bmp = New-Object System.Drawing.Bitmap $Size, $Size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# ------------------------------------------------------------------ 背景
$bgRect = New-Object System.Drawing.Rectangle 0, 0, $Size, $Size
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bgRect, ([System.Drawing.Color]::FromArgb(255, 46, 26, 22)), ([System.Drawing.Color]::FromArgb(255, 12, 8, 8)), 90.0
$g.FillRectangle($bgBrush, $bgRect)
$bgBrush.Dispose()

$bandRect = New-Object System.Drawing.Rectangle 0, 0, $Size, 150
$bandBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $bandRect, ([System.Drawing.Color]::FromArgb(190, 96, 24, 28)), ([System.Drawing.Color]::FromArgb(0, 96, 24, 28)), 90.0
$g.FillRectangle($bandBrush, $bandRect)
$bandBrush.Dispose()

# ------------------------------------------------------------------ 标题
$titleFont = New-Object System.Drawing.Font 'Microsoft YaHei', 40, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$subFont = New-Object System.Drawing.Font 'Microsoft YaHei', 18, ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
$shadowBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(200, 0, 0, 0))
$titleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 244, 219, 168))
$subBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 190, 146, 90))
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

$title = '让酿酒再次伟大'
$sub = 'make brewer great again'
$g.DrawString($title, $titleFont, $shadowBrush, (New-Object System.Drawing.RectangleF 3, 25, $Size, 52), $fmt)
$g.DrawString($title, $titleFont, $titleBrush, (New-Object System.Drawing.RectangleF 0, 22, $Size, 52), $fmt)
$g.DrawString($sub, $subFont, $subBrush, (New-Object System.Drawing.RectangleF 0, 74, $Size, 26), $fmt)

$linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 190, 146, 90)), 2
$g.DrawLine($linePen, 100, 112, $Size - 100, 112)
$linePen.Dispose()

# ------------------------------------------------------------------ 第一排：营养果 / 营养果块 / 营养果碎
$fruits = @('nutrifruit.png', 'cutten_nutrifruit.png', 'mashed_nutrifruit.png')
$fruitScale = 3
$fruitGap = 30
$fruitImgs = @()
$fruitW = 0
foreach ($n in $fruits) {
	$img = Get-Sprite $n
	if ($null -eq $img) { continue }
	$fruitImgs += $img
	$fruitW += $img.Width * $fruitScale
}
if ($fruitImgs.Count -gt 0) {
	$fruitW += $fruitGap * ($fruitImgs.Count - 1)
	$fx = [int](($Size - $fruitW) / 2)
	$fy = 124
	foreach ($img in $fruitImgs) {
		$w = $img.Width * $fruitScale
		$h = $img.Height * $fruitScale
		$g.DrawImage($img, (New-Object System.Drawing.Rectangle $fx, $fy, $w, $h))
		$fx += $w + $fruitGap
		$img.Dispose()
	}
}

# ------------------------------------------------------------------ 第二排：酒瓶 酒瓶 [名片] 酒瓶 酒瓶
$bottleNames = @('Premium craft beer.png', 'craft beer.png', 'homebrewed beer.png', 'homebrewed poison.png')
$bottleScale = 3
$bottleGap = 20
$bottleY = 214
$cardScale = 5

$bottles = @()
foreach ($n in $bottleNames) {
	$img = Get-Sprite $n
	if ($null -eq $img) { continue }
	$bottles += $img
}
$card = Get-Sprite 'card.png'

$cardW = 0
$cardH = 0
if ($null -ne $card) { $cardW = $card.Width * $cardScale; $cardH = $card.Height * $cardScale }

# 瓶子左右各一半，名片夹在正中间
$leftCount = [int][Math]::Ceiling($bottles.Count / 2)
$perSideW = 0
if ($bottles.Count -gt 0) {
	$perSideW = $bottles[0].Width * $bottleScale * $leftCount + $bottleGap * ($leftCount - 1)
}
$sideGap = 26
$totalW = $perSideW * 2 + $cardW + $sideGap * 2
$bx = [int](($Size - $totalW) / 2)

$bottleH = 0
$i = 0
foreach ($img in $bottles) {
	if ($i -eq $leftCount) { $bx += $cardW + $sideGap * 2 }
	$w = $img.Width * $bottleScale
	$h = $img.Height * $bottleScale
	if ($h -gt $bottleH) { $bottleH = $h }
	$g.DrawImage($img, (New-Object System.Drawing.Rectangle $bx, $bottleY, $w, $h))
	$bx += $w + $bottleGap
	$img.Dispose()
	$i++
}

# ------------------------------------------------------------------ 名片：正对画面中心
if ($null -ne $card) {
	$cardX = [int](($Size - $cardW) / 2)
	$cardY = $bottleY + [int](($bottleH - $cardH) / 2)

	$g.DrawImage($card, (New-Object System.Drawing.Rectangle $cardX, $cardY, $cardW, $cardH))
	$card.Dispose()
}

# ------------------------------------------------------------------ 底部：恒温发酵机 + 啤酒花
$machine = Get-Sprite '发酵机.png'
if ($null -ne $machine) {
	$scale = 2
	$mw = $machine.Width * $scale
	$mh = $machine.Height * $scale
	$hops = Get-Sprite 'hops.png'
	$hs = 4
	$hopsW = 0
	if ($null -ne $hops) { $hopsW = 16 * $hs + 24 }
	$groupW = $mw + $hopsW
	$mx = [int](($Size - $groupW) / 2)
	$my = $Size - $mh - 14
	$g.DrawImage($machine, (New-Object System.Drawing.Rectangle $mx, $my, $mw, $mh))
	$machine.Dispose()
	if ($null -ne $hops) {
		$g.DrawImage($hops, (New-Object System.Drawing.Rectangle ($mx + $mw + 24), ($my + $mh - (16 * $hs)), (16 * $hs), (16 * $hs)))
		$hops.Dispose()
	}
}

# ------------------------------------------------------------------ 保存
$enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/png' }
$params = New-Object System.Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
$bmp.Save($OutFile, $enc, $params)

$titleFont.Dispose(); $subFont.Dispose()
$shadowBrush.Dispose(); $titleBrush.Dispose(); $subBrush.Dispose(); $fmt.Dispose()
$g.Dispose(); $bmp.Dispose()

$info = Get-Item $OutFile
Write-Host ("已生成 {0}（{1} 字节）" -f $info.FullName, $info.Length)
