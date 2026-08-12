# 启动模拟客机(假扮第二个玩家),用于单机测试联机同步。
#
# 前提:游戏已启动,并在游戏内按过 F6(回环主机)。
#
#   .\sim.ps1                       默认参数
#   .\sim.ps1 -Name 小明 -Radius 5   自定义
param(
    [string]$Name = "模拟客机",
    [double]$Radius = 3,
    [double]$Speed = 1.2
)

$proj = "$PSScriptRoot\tools\CoopSimClient\CoopSimClient.csproj"
$exe = "$PSScriptRoot\tools\CoopSimClient\bin\Release\net8.0\CoopSimClient.exe"

if (-not (Test-Path $exe) -or (Get-Item $proj).LastWriteTime -gt (Get-Item $exe).LastWriteTime) {
    Write-Host "▸ 编译模拟客机" -ForegroundColor Cyan
    dotnet build $proj -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not (Get-Process -Name DolocTown -ErrorAction SilentlyContinue)) {
    Write-Host "提示:游戏没在运行。先 .\dev.ps1 启动游戏,进存档后按 F6 开回环主机。" -ForegroundColor Yellow
}

& $exe --name $Name --radius $Radius --speed $Speed
