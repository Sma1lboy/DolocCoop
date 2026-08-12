# 跑 CoopCore 的自动化测试(纯逻辑,不需要游戏)。
#   .\test.ps1
# 失败会以非零退出码结束,可以挂到提交前检查。
$ErrorActionPreference = "Stop"
dotnet run --project "$PSScriptRoot\tools\CoopCoreTests\CoopCoreTests.csproj" -c Release --no-launch-profile
exit $LASTEXITCODE
