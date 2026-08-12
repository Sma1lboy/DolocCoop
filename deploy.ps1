# 把 mods\<Mod名> 部署到游戏本地 MODS 目录
# 用法: .\deploy.ps1 <Mod文件夹名>   (省略参数则列出可部署的 Mod)
param([string]$ModName)

$src = Join-Path $PSScriptRoot "mods"
$dst = "$env:USERPROFILE\AppData\LocalLow\RedSawGames\DolocTown\MODS"

if (-not $ModName) {
    Write-Host "可部署的 Mod:" -ForegroundColor Cyan
    Get-ChildItem $src -Directory | ForEach-Object { "  $($_.Name)" }
    exit 0
}

$modPath = Join-Path $src $ModName
if (-not (Test-Path (Join-Path $modPath "info.json"))) {
    Write-Error "找不到 $modPath\info.json — 请确认 Mod 文件夹名,且包含 info.json"
    exit 1
}

$target = Join-Path $dst $ModName
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
# _IGNORE 文件夹按官方约定不打包
Copy-Item $modPath $target -Recurse
Get-ChildItem $target -Directory -Filter "_IGNORE" -Recurse | Remove-Item -Recurse -Force

Write-Host "已部署到 $target" -ForegroundColor Green
Write-Host "启动游戏 → Mod 菜单 → Local 标签启用。测试前记得备份存档 SAVE\ea-doloc-archive-0.data"
