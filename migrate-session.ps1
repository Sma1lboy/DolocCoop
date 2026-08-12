# 把本次 Claude 会话完整移动到 DolocTownMods 项目下。
# 请在【退出当前 Claude 会话之后】运行本脚本,然后在本目录执行 claude --continue 恢复对话。
$sid = "42289706-5668-4017-8710-0258fd79bacf"
$src = "$env:USERPROFILE\.claude\projects\C--Users-jackson\$sid.jsonl"
$dstDir = "$env:USERPROFILE\.claude\projects\C--Users-jackson-DolocTownMods"

if (Test-Path $src) {
    New-Item -ItemType Directory -Force $dstDir | Out-Null
    Move-Item $src (Join-Path $dstDir "$sid.jsonl") -Force
    Write-Host "会话已移动。现在在 DolocTownMods 目录运行: claude --continue" -ForegroundColor Green
} else {
    Write-Host "源会话文件不存在(可能已移动过)。" -ForegroundColor Yellow
}
