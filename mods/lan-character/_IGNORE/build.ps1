# 小澜角色 Mod 的完整构建链。改任何参数都跑这个,别单独跑子脚本。
#
#   .\build.ps1                 用定稿参数重建全部素材
#   .\build.ps1 -NoTail         只换色,不接长发
#   .\build.ps1 -Deploy         建完直接部署到游戏 MODS 目录
#
# 为什么必须走这里:add-ponytail 是在已有发丝的底端往下接,
# 它对同一批文件重复运行会越接越长。gen-character 每次从官方原始帧重新生成,
# 所以"先 gen 再 tail"这个顺序保证了幂等 —— 单独重跑 add-ponytail 不行。
param(
    [switch]$NoTail,
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot

# ---- 定稿参数(用户 2026-08-12 选定:焦糖挑染)----
$HAIR      = "#241F26"   # 主色。取自 lan-imag 仓库 approved-gamechar.jpg 的发色 #201E22,略提亮
$HIGHLIGHT = "#A87B4E"   # 焦糖挑染,落在刘海与鬓边
$DARKF     = 0.55
$TAILLEN   = 9

Write-Host "[1/4] 从官方原始帧重新生成 144 帧并换色" -ForegroundColor Cyan
& (Join-Path $here "gen-character.ps1") -HairHex $HAIR -HighlightHex $HIGHLIGHT -DarkFactor $DARKF | Out-Null

if (-not $NoTail) {
    Write-Host "[2/4] 接长侧发" -ForegroundColor Cyan
    & (Join-Path $here "add-ponytail.ps1") -Length $TAILLEN
} else {
    Write-Host "[2/4] 跳过接发(-NoTail)" -ForegroundColor DarkGray
}

# 角色头像。原图是 AI 生成的 1024 大图,不入库(见 .gitignore),
# 丢了就重跑 lan-imag 仓库的 jobs/doloc-portrait-r2.json
$portraitSrc = Join-Path $here "portrait-src.png"
if (Test-Path $portraitSrc) {
    Write-Host "[3/4] 生成角色头像 icon_character_player.png" -ForegroundColor Cyan
    & (Join-Path $here "gen-portrait.ps1") -Src $portraitSrc `
        -Out (Join-Path (Split-Path $here -Parent) "Content\Texture\icon_character_player.png")
} else {
    Write-Host "[3/4] 跳过头像:缺 _IGNORE\portrait-src.png" -ForegroundColor DarkYellow
}

Write-Host "[4/4] 生成 icon.png / preview.png" -ForegroundColor Cyan
& (Join-Path $here "gen-icons.ps1")

if ($Deploy) {
    $repo = Split-Path (Split-Path (Split-Path $here -Parent) -Parent) -Parent
    & (Join-Path $repo "deploy.ps1") "lan-character"
}


