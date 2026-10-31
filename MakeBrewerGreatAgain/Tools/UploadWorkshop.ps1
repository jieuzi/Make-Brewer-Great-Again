<#
  用 steamcmd 上传/更新 Steam 创意工坊物品（Probably Stolen，appid 4348910）。

  首次上传：
      powershell -NoProfile -ExecutionPolicy Bypass -File Tools\UploadWorkshop.ps1 -Account 你的Steam账号

  想一次性带上密码（可以少问一次）：
      ... -Account 账号 -Password 密码

  以后更新（先在 Workshop\workshop_item.vdf 里把 publishedfileid 改成真实 ID，或者这里直接给）：
      ... -Account 账号 -PublishedFileId 3487xxxxxx

  公开物品（默认是私密 2）：
      ... -Account 账号 -Visibility 0
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory = $true)][string]$Account,
	[string]$Password = '',
	[string]$SteamCmd = 'D:\steamcmd\steamcmd.exe',
	[string]$Vdf = '',
	[string]$PublishedFileId = '',
	[int]$Visibility = -1,
	[string]$Changenote = '',
	[switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- 路径
$ToolsDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModDir = Split-Path -Parent $ToolsDir
if (-not $Vdf) { $Vdf = Join-Path $ModDir 'Workshop\workshop_item.vdf' }

if (-not (Test-Path $SteamCmd)) { throw "找不到 steamcmd：$SteamCmd（用 -SteamCmd 指定路径）" }
if (-not (Test-Path $Vdf)) { throw "找不到 VDF：$Vdf" }

function Get-VdfValue([string]$text, [string]$key) {
	$m = [regex]::Match($text, '"' + [regex]::Escape($key) + '"\s+"([^"]*)"')
	if ($m.Success) { return $m.Groups[1].Value }
	return $null
}

function Set-VdfValue([string]$text, [string]$key, [string]$value) {
	$pattern = '("' + [regex]::Escape($key) + '"\s+")[^"]*(")'
	if ([regex]::IsMatch($text, $pattern)) {
		return [regex]::Replace($text, $pattern, ('${1}' + $value.Replace('$', '$$') + '${2}'), 1)
	}
	return $text
}

# ---------------------------------------------------------------- 参数写回 VDF
$text = [System.IO.File]::ReadAllText($Vdf, [System.Text.Encoding]::UTF8)
$changed = $false

if ($PublishedFileId) {
	$text = Set-VdfValue $text 'publishedfileid' $PublishedFileId
	$changed = $true
}
if ($Visibility -ge 0) {
	$text = Set-VdfValue $text 'visibility' "$Visibility"
	$changed = $true
}
if ($Changenote) {
	$text = Set-VdfValue $text 'changenote' $Changenote
	$changed = $true
}
if ($changed) {
	[System.IO.File]::WriteAllText($Vdf, $text, [System.Text.UTF8Encoding]::new($false))
	Write-Host "已更新 VDF：$Vdf"
}

# ---------------------------------------------------------------- 校验内容
$content = Get-VdfValue $text 'contentfolder'
$preview = Get-VdfValue $text 'previewfile'
$pfid = Get-VdfValue $text 'publishedfileid'

Write-Host ''
Write-Host '=== 上传信息 ==='
Write-Host ("appid           : {0}" -f (Get-VdfValue $text 'appid'))
Write-Host ("publishedfileid : {0}" -f $pfid)
Write-Host ("contentfolder   : {0}" -f $content)
Write-Host ("previewfile     : {0}" -f $preview)
Write-Host ("visibility      : {0}" -f (Get-VdfValue $text 'visibility'))
Write-Host ''

if (-not (Test-Path $content)) { throw "内容目录不存在：$content" }
if (-not (Test-Path (Join-Path $content 'manifest.xml'))) {
	throw "内容目录根下没有 manifest.xml。创意工坊物品的根目录本身就是一个模组文件夹，缺了它游戏不会认。"
}
$dlls = @(Get-ChildItem $content -Filter *.dll -File)
if ($dlls.Count -eq 0) { Write-Host '警告：内容目录里没有 DLL，确认清单里的 <Assemblies> 是否写错。' }
$bytes = (Get-ChildItem $content -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("内容大小        : {0:N2} MB，{1} 个 DLL" -f ($bytes / 1MB), $dlls.Count)

if (-not $preview -or -not (Test-Path $preview)) {
	Write-Host '警告：没有预览图，Steam 上会显示占位图（可以运行 Tools\MakeWorkshopPreview.ps1 生成）。'
}
else {
	$pi = Get-Item $preview
	if ($pi.Length -gt 1MB) { throw "预览图超过 1 MB（$([math]::Round($pi.Length/1KB)) KB），Steam 会拒绝。" }
	try {
		Add-Type -AssemblyName System.Drawing
		$img = [System.Drawing.Image]::FromFile($pi.FullName)
		Write-Host ("预览图          : {0}x{1}，{2} KB" -f $img.Width, $img.Height, [math]::Round($pi.Length / 1KB))
		$img.Dispose()
	}
	catch { Write-Host "预览图          : $($pi.Length) 字节（无法读取尺寸）" }
}

if ($DryRun) { Write-Host ''; Write-Host 'DryRun：只做了检查，没有调用 steamcmd。'; return }

# ---------------------------------------------------------------- 调 steamcmd
$logFile = Join-Path (Split-Path -Parent $SteamCmd) 'logs\workshop_log.txt'
if (Test-Path $logFile) { Remove-Item $logFile -Force -ErrorAction SilentlyContinue }

Write-Host ''
Write-Host '=== 调用 steamcmd（首次登录会要求输入密码和 Steam 令牌）==='
Write-Host ''

$steamArgs = @('+login', $Account)
if ($Password) { $steamArgs += $Password }
$steamArgs += @('+workshop_build_item', $Vdf, '+quit')

& $SteamCmd @steamArgs
$code = $LASTEXITCODE

Write-Host ''
if ($code -ne 0) { Write-Host "steamcmd 退出码 $code —— 上传很可能失败，看上面的输出找原因。" }
else { Write-Host "steamcmd 退出码 0" }

# ---------------------------------------------------------------- 找新的 PublishedFileId
$newId = ''
foreach ($candidate in @($logFile)) {
	if ($candidate -and (Test-Path $candidate)) {
		$logText = Get-Content $candidate -Raw -ErrorAction SilentlyContinue
		$m = [regex]::Match($logText, '(?i)(?:published\s*file\s*id|publishedfileid|new\s*workshop\s*item[^\d]{0,40})(\d{6,})')
		if ($m.Success) { $newId = $m.Groups[1].Value; break }
	}
}

Write-Host ''
if ($newId) {
	Write-Host "新的 PublishedFileId：$newId"
	Write-Host "已写回 $Vdf，下次直接跑同样的命令就是更新这个物品。"
	$text2 = [System.IO.File]::ReadAllText($Vdf, [System.Text.Encoding]::UTF8)
	$text2 = Set-VdfValue $text2 'publishedfileid' $newId
	[System.IO.File]::WriteAllText($Vdf, $text2, [System.Text.UTF8Encoding]::new($false))
}
elseif (-not $pfid -or $pfid -eq '0') {
	Write-Host '没从日志里读到新 ID。到 https://steamcommunity.com/my/workshopfiles/?appid=4348910 看一下，'
	Write-Host '把物品 ID 填进 Workshop\workshop_item.vdf 的 publishedfileid，或者下次用 -PublishedFileId 传进来。'
}
else {
	Write-Host "更新了已有物品 $pfid。"
}

Write-Host ''
Write-Host '完成后：订阅自己的物品 → 重启游戏 → 模组列表里应出现 Workshop 来源的同名模组。'
Write-Host '注意：Mods\MakeBrewerGreatAgain 里的本地副本优先级更高，会盖住工坊副本（日志里会写 local copy wins）。'
