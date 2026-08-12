# 构建 DolocCoop(+ 可选 DolocDevTools)并部署到游戏 BepInEx\plugins
# 前提: 游戏目录已装 BepInEx(DTMAPI 安装器会装)+ 本机有 dotnet SDK
# 注意: 游戏运行时 DLL 被内存映射锁定,无法覆盖 —— 必须先关掉游戏
param(
    [switch]$NoDeploy,
    [switch]$All        # 连同 DolocDevTools 一起构建部署
)

$game = $env:DOLOC_TOWN_PATH
if (-not $game) { $game = "C:\Program Files (x86)\Steam\steamapps\common\Doloc Town" }

if (-not (Test-Path "$game\BepInEx\core\BepInEx.dll")) {
    Write-Error "未找到 $game\BepInEx — 请先运行 DTMAPI 安装器(会装 BepInEx 5),或手动安装 BepInEx 5.4.x"
    exit 1
}

if (-not $NoDeploy) {
    $proc = Get-Process -Name DolocTown -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Host "游戏正在运行 (PID $($proc.Id)) — DLL 被锁定,无法部署。" -ForegroundColor Yellow
        Write-Host "请关闭游戏后重跑本脚本;或加 -NoDeploy 只编译不部署。" -ForegroundColor Yellow
        exit 1
    }
}

$projects = @(@{ Name = "DolocCoop"; Path = "$PSScriptRoot\src\DolocCoop\DolocCoop.csproj"; Out = "$PSScriptRoot\src\DolocCoop\bin\Release"; Files = @("DolocCoop.dll", "CoopCore.dll") })
if ($All) {
    $dev = Split-Path $PSScriptRoot -Parent
    $projects += @{ Name = "DolocDevTools"; Path = "$dev\doloc-devtools\src\DolocDevTools.csproj"; Out = "$dev\doloc-devtools\src\bin\Release"; Files = @("DolocDevTools.dll") }
}

foreach ($p in $projects) {
    dotnet build $p.Path -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (-not $NoDeploy) {
        $dst = "$game\BepInEx\plugins\$($p.Name)"
        New-Item -ItemType Directory -Force $dst | Out-Null
        foreach ($f in $p.Files) { Copy-Item (Join-Path $p.Out $f) $dst -Force }
        Write-Host "已部署 $($p.Name) → $dst" -ForegroundColor Green
    }
}
