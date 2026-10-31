# 安装前把 manifest.xml 统一转成 UTF-8（无 BOM）。
# 游戏用严格 UTF-8 解析清单：源文件被编辑器存成 GBK/ANSI 时，装出去会变成
# "Invalid character in the given encoding" 而直接跳过整个模组。
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
)

if (-not (Test-Path -LiteralPath $Source)) {
    Write-Error "找不到清单文件：$Source"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($Source)
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)

$text = $null
$from = 'UTF-8'
try {
    $text = $utf8Strict.GetString($bytes)
}
catch {
    $from = 'GBK/ANSI'
    try {
        $text = [System.Text.Encoding]::GetEncoding(936).GetString($bytes)
    }
    catch {
        Write-Error "无法解码 $Source 的编码，请用 UTF-8 保存后重试。"
        exit 1
    }
}

$text = $text.TrimStart([char]0xFEFF)

$dir = Split-Path -Parent $Destination
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}
[System.IO.File]::WriteAllText($Destination, $text, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "manifest 编码：$from -> UTF-8"
exit 0