# 精灵表切分与规范化
#
# 用途:图像 API 一次生成一张含 N 帧的精灵表(同一次生成 → 跨帧一致,
# 这是它相对"一帧一次调用"的关键优势),本脚本负责把它变成游戏能用的帧。
#
#   .\sheet-to-frames.ps1 -Sheet walk.png -Action walk -Cols 4
#   .\sheet-to-frames.ps1 -Sheet idle.png -Action idle -Cols 3 -Layer body
#
# 做的四件事:
#   1. 按列数等分切帧
#   2. 去背景(取四角颜色作为背景色,容差内的像素设为透明)
#   3. 量化调色板(像素风的关键 —— API 出的图有大量抗锯齿中间色,不量化会糊)
#   4. 对齐并缩放到 64x64,底部对齐(游戏的主角帧是底部留 16px)
param(
    [Parameter(Mandatory=$true)][string]$Sheet,
    [Parameter(Mandatory=$true)][string]$Action,
    [int]$Cols = 4,
    [int]$Rows = 1,
    [ValidateSet("hair","body","")] [string]$Layer = "hair",
    [int]$Colors = 6,
    [int]$BgTolerance = 60,
    [int]$BottomMargin = 16,
    [int]$TopMargin = 6,
    # 默认写正式目录;测试时务必指定别的目录,否则会覆盖已生成的帧
    [string]$OutDir = ""
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

if (-not (Test-Path $Sheet)) { Write-Error "找不到精灵表: $Sheet"; exit 1 }
$outDir = if ($OutDir) { $OutDir } else { Join-Path (Split-Path $PSScriptRoot -Parent) "Content\Texture" }
New-Item -ItemType Directory -Force $outDir | Out-Null

# 用绝对路径:.NET 的当前目录和 PowerShell 的位置不是一回事,相对路径会读不到
$sheetPath = (Resolve-Path $Sheet).Path
# 变量名不要和参数 $Sheet 撞 —— PowerShell 变量不区分大小写,会互相覆盖
$img = [System.Drawing.Bitmap]::new($sheetPath)
Write-Host "精灵表 $($img.Width)x$($img.Height),切成 $Cols x $Rows 帧" -ForegroundColor Cyan

# ---- 背景色:取四角的平均,API 出图通常是纯色或渐变背景 ----
function CornerBg($bmp) {
    # 每个坐标都要括起来:PowerShell 里逗号优先级高于减号,
    # 写成 @($bmp.Width-1,0) 会被算成 $bmp.Width - @(1,0)
    $w1 = $bmp.Width - 1; $h1 = $bmp.Height - 1
    $pts = @(@(0,0), @($w1,0), @(0,$h1), @($w1,$h1))
    $r=0;$g=0;$b=0
    foreach ($p in $pts) { $c = $bmp.GetPixel($p[0],$p[1]); $r+=$c.R; $g+=$c.G; $b+=$c.B }
    return [System.Drawing.Color]::FromArgb(255, [int]($r/4), [int]($g/4), [int]($b/4))
}
$bg = CornerBg $img
Write-Host ("背景色: #{0:X2}{1:X2}{2:X2}(容差 {3})" -f $bg.R,$bg.G,$bg.B,$BgTolerance)

function IsBg($c, $bg, $tol) {
    return ([Math]::Abs($c.R-$bg.R) -le $tol -and [Math]::Abs($c.G-$bg.G) -le $tol -and [Math]::Abs($c.B-$bg.B) -le $tol)
}

# ---- 量化:把颜色收敛到 N 个,像素风才立得住 ----
function Quantize($bmp, $n) {
    $buckets = @{}
    for ($y=0;$y -lt $bmp.Height;$y++) { for ($x=0;$x -lt $bmp.Width;$x++) {
        $c = $bmp.GetPixel($x,$y); if ($c.A -eq 0) { continue }
        $k = "$([int]($c.R/32)),$([int]($c.G/32)),$([int]($c.B/32))"
        if (-not $buckets.ContainsKey($k)) { $buckets[$k] = @{n=0;r=0;g=0;b=0} }
        $buckets[$k].n++; $buckets[$k].r+=$c.R; $buckets[$k].g+=$c.G; $buckets[$k].b+=$c.B
    }}
    $pal = $buckets.GetEnumerator() | Sort-Object { $_.Value.n } -Descending | Select-Object -First $n |
        ForEach-Object { [System.Drawing.Color]::FromArgb(255, [int]($_.Value.r/$_.Value.n), [int]($_.Value.g/$_.Value.n), [int]($_.Value.b/$_.Value.n)) }
    if ($pal.Count -eq 0) { return }
    for ($y=0;$y -lt $bmp.Height;$y++) { for ($x=0;$x -lt $bmp.Width;$x++) {
        $c = $bmp.GetPixel($x,$y); if ($c.A -eq 0) { continue }
        $best=$null; $bd=[int]::MaxValue
        foreach ($p in $pal) {
            $d = ($c.R-$p.R)*($c.R-$p.R)+($c.G-$p.G)*($c.G-$p.G)+($c.B-$p.B)*($c.B-$p.B)
            if ($d -lt $bd) { $bd=$d; $best=$p }
        }
        $bmp.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($c.A,$best.R,$best.G,$best.B))
    }}
}

$prefix = if ($Layer) { "anim_player_${Layer}_${Action}" } else { "anim_player_${Action}" }
$fw = [int]($img.Width / $Cols); $fh = [int]($img.Height / $Rows)

# ---- 第一遍:切帧、去背景,并求所有帧的【并集】包围盒 ----
#
# 关键:包围盒必须是全帧共用的,不能逐帧裁。
# 逐帧裁会把帧间的相对位移吃掉 —— 走路的上下起伏、挥手时身体的偏移,
# 每帧都被重新居中/贴底之后就全没了,动画会变成"原地抽搐"。
$cells = @()
$uMinX = [int]::MaxValue; $uMinY = [int]::MaxValue; $uMaxX = -1; $uMaxY = -1

for ($ry=0; $ry -lt $Rows; $ry++) {
  for ($cx=0; $cx -lt $Cols; $cx++) {
    $cell = [System.Drawing.Bitmap]::new($fw, $fh)
    $g = [System.Drawing.Graphics]::FromImage($cell)
    $g.DrawImage($img, ([System.Drawing.Rectangle]::new(0,0,$fw,$fh)),
                 ([System.Drawing.Rectangle]::new($cx*$fw, $ry*$fh, $fw, $fh)), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    # 去背景。注意:方法调用要先落到变量再传参 —— 写成 `IsBg $cell.GetPixel($x,$y) ...`
    # 会被当成命令参数解析,$cell.GetPixel 和 ($x,$y) 被拆成两个参数
    for ($y=0;$y -lt $cell.Height;$y++) { for ($x=0;$x -lt $cell.Width;$x++) {
        $px = $cell.GetPixel($x,$y)
        if (IsBg $px $bg $BgTolerance) { $cell.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(0,0,0,0)) }
        else {
            if ($x -lt $uMinX){$uMinX=$x}; if ($x -gt $uMaxX){$uMaxX=$x}
            if ($y -lt $uMinY){$uMinY=$y}; if ($y -gt $uMaxY){$uMaxY=$y}
        }
    }}
    $cells += $cell
  }
}

if ($uMaxX -lt 0) { Write-Error "整张精灵表都被判成背景了 —— 试试调小 -BgTolerance"; exit 1 }

$cw = $uMaxX - $uMinX + 1; $ch = $uMaxY - $uMinY + 1
$targetH = 64 - $BottomMargin - $TopMargin
$scale = $targetH / $ch
$nw = [Math]::Max(1, [int]($cw * $scale)); $nh = [Math]::Max(1, [int]($ch * $scale))
$dstX = [int]((64 - $nw)/2)
$dstY = 64 - $BottomMargin - $nh
Write-Host "共用包围盒: ${cw}x${ch} @($uMinX,$uMinY) → ${nw}x${nh} @($dstX,$dstY)" -ForegroundColor Cyan

# ---- 第二遍:所有帧用同一个盒子、同一个缩放比落到 64x64 ----
$srcRect = [System.Drawing.Rectangle]::new($uMinX, $uMinY, $cw, $ch)
$dstRect = [System.Drawing.Rectangle]::new($dstX, $dstY, $nw, $nh)
$idx = 0
foreach ($cell in $cells) {
    $frame = [System.Drawing.Bitmap]::new(64, 64)
    $g2 = [System.Drawing.Graphics]::FromImage($frame)
    $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g2.PixelOffsetMode  = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g2.DrawImage($cell, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    $g2.Dispose()

    Quantize $frame $Colors

    $name = "${prefix}_${idx}.png"
    $frame.Save((Join-Path $outDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "  $name"
    $frame.Dispose(); $cell.Dispose()
    $idx++
}
$img.Dispose()
Write-Host "完成:$idx 帧 → $outDir" -ForegroundColor Green



