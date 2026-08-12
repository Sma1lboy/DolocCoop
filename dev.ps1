# DolocTown Mod 一键开发循环:关游戏 → 编译 → 部署 → 启动 → (可选)等 dump
#
#   .\dev.ps1                 编译部署内容Mod与插件,然后启动游戏
#   .\dev.ps1 -Wait           启动后等待自动 dump 产出并列出文件
#   .\dev.ps1 -NoLaunch       只编译部署,不启动
#   .\dev.ps1 -Dual           双开(回环联机测试;会写入 steam_appid.txt 以便直接启动 exe)
#   .\dev.ps1 -Clean          启动前清空 debug dump 目录
param(
    [switch]$Wait,
    [switch]$NoLaunch,
    [switch]$Dual,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$game    = if ($env:DOLOC_TOWN_PATH) { $env:DOLOC_TOWN_PATH } else { "C:\Program Files (x86)\Steam\steamapps\common\Doloc Town" }
$appId   = "2285550"
$debugDir = "$env:USERPROFILE\AppData\LocalLow\RedSawGames\DolocTown\DolocCoop-debug"
$modsDir  = "$env:USERPROFILE\AppData\LocalLow\RedSawGames\DolocTown\MODS"

function Step($msg) { Write-Host "▸ $msg" -ForegroundColor Cyan }

# ---- 1. 关闭运行中的游戏(DLL 被内存映射锁定,不关无法部署) ----
$proc = Get-Process -Name DolocTown -ErrorAction SilentlyContinue
if ($proc) {
    Step "关闭运行中的游戏 (PID $($proc.Id))"
    $proc | Stop-Process
    Start-Sleep -Milliseconds 1500
}

# ---- 2. 编译并部署代码插件 ----
Step "编译并部署代码插件"
& "$PSScriptRoot\mods-code\doloc-coop\build.ps1" -All
if ($LASTEXITCODE -ne 0) { Write-Error "插件构建失败"; exit 1 }

# ---- 3. 部署内容 Mod(JSON) ----
Step "部署内容 Mod"
Get-ChildItem "$PSScriptRoot\mods" -Directory | ForEach-Object {
    if (Test-Path (Join-Path $_.FullName "info.json")) {
        & "$PSScriptRoot\deploy.ps1" $_.Name | Out-Null
        Write-Host "    $($_.Name)"
    }
}

# ---- 4. 清理 dump ----
if ($Clean -and (Test-Path $debugDir)) {
    Step "清空 debug dump 目录"
    Remove-Item "$debugDir\*" -Force -ErrorAction SilentlyContinue
}

if ($NoLaunch) { Step "跳过启动 (-NoLaunch)"; exit 0 }

# ---- 5. 启动 ----
if ($Dual) {
    # 双开需要直接启动 exe(Steam 客户端不会开第二个实例)。
    # steam_appid.txt 让直接启动的进程也能初始化 Steam API。
    $appIdFile = Join-Path $game "steam_appid.txt"
    if (-not (Test-Path $appIdFile)) {
        Step "写入 steam_appid.txt(双开直启需要)"
        Set-Content $appIdFile $appId -NoNewline
    }
    Step "启动实例 A"
    Start-Process (Join-Path $game "DolocTown.exe") -WorkingDirectory $game
    Start-Sleep -Seconds 8
    Step "启动实例 B"
    Start-Process (Join-Path $game "DolocTown.exe") -WorkingDirectory $game
    Write-Host ""
    Write-Host "双开已启动。测试步骤:" -ForegroundColor Yellow
    Write-Host "  实例A: 进存档位1 → F11 (回环主机)"
    Write-Host "  实例B: 进存档位2 → Ctrl+F11 (连接主机)"
    Write-Host "  两端日志: $debugDir\net-<PID>.log"
} else {
    Step "通过 Steam 启动游戏"
    Start-Process "steam://rungameid/$appId"
}

# ---- 6. 等待自动 dump ----
if ($Wait) {
    Step "等待自动 dump 产出(最多 90 秒)"
    $deadline = (Get-Date).AddSeconds(90)
    $seen = @{}
    while ((Get-Date) -lt $deadline) {
        if (Test-Path $debugDir) {
            Get-ChildItem $debugDir -Filter "latest-*.txt" | ForEach-Object {
                if (-not $seen.ContainsKey($_.Name) -and $_.Length -gt 0) {
                    $seen[$_.Name] = $true
                    Write-Host "    ✔ $($_.Name)  ($([int]($_.Length/1024)) KB)" -ForegroundColor Green
                }
            }
        }
        if ($seen.Count -ge 2) { break }
        Start-Sleep -Seconds 3
    }
    if ($seen.Count -eq 0) { Write-Host "    (超时未产出 — 游戏可能还在加载)" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
