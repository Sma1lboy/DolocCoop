# 角色 Mod 生成器 —— 把主角的头发换成指定发色,并从参考图生成头像。
#
#   .\gen-character.ps1 -HairHex "#8B4A2B"                     只换发色
#   .\gen-character.ps1 -Ref "C:\path\lan.jpg"                 从参考图取发色 + 生成头像
#   .\gen-character.ps1 -Ref "...\lan.jpg" -HairHex "#3A2A20"  参考图做头像,发色手动指定
#
# 原理:官方主角头发贴图只有 6 种颜色,其中 3 个是主色调(亮/中/暗)。
# 把这 3 档映射到新发色的 3 档,144 帧一次性全部换掉,
# 明暗关系保持不变,所以动画看起来仍然是原来那套(不会闪、不会错位)。
param(
    [string]$Ref = "",
    [string]$HairHex = "",
    [switch]$SkipFrames
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$modRoot = Split-Path $PSScriptRoot -Parent
$texOut  = Join-Path $modRoot "Content\Texture"
$srcRoot = "C:\Program Files (x86)\Steam\steamapps\workshop\content\2285550\3705665433\Content\04 其他内容示例 Miscellaneous\03 备用贴图 Image Assets\02 主角动画 Character Animations\_IGNORE\01 头发动画 Hair Animations"

if (-not (Test-Path $srcRoot)) {
    Write-Error "找不到官方示例模组的主角帧。请先在创意工坊订阅【Dev】Example Mod。`n期望路径: $srcRoot"
    exit 1
}
New-Item -ItemType Directory -Force $texOut | Out-Null

# 原始发色的三档(实测自 anim_player_hair_idle_0.png)
$srcMid  = [System.Drawing.Color]::FromArgb(255, 49, 40, 62)   # 主色
$srcDark = [System.Drawing.Color]::FromArgb(255, 34, 29, 40)   # 暗部
$srcHigh = [System.Drawing.Color]::FromArgb(255, 58, 33, 53)   # 反光

function ColorDist($a, $b) {
    $dr = $a.R - $b.R; $dg = $a.G - $b.G; $db = $a.B - $b.B
    return $dr*$dr + $dg*$dg + $db*$db
}

function Shift($c, [double]$factor) {
    $r = [Math]::Min(255, [Math]::Max(0, [int]($c.R * $factor)))
    $g = [Math]::Min(255, [Math]::Max(0, [int]($c.G * $factor)))
    $b = [Math]::Min(255, [Math]::Max(0, [int]($c.B * $factor)))
    return [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
}

# ---------- 取目标发色 ----------
function DominantHairColor([string]$path) {
    # 取图像上半部分(头发通常在这)的主色调,忽略过亮/过暗的像素(高光与阴影)
    $img = [System.Drawing.Bitmap]::new($path)
    try {
        $buckets = @{}
        $yMax = [int]($img.Height * 0.45)
        for ($y = 0; $y -lt $yMax; $y += 2) {
            for ($x = 0; $x -lt $img.Width; $x += 2) {
                $p = $img.GetPixel($x, $y)
                if ($p.A -lt 200) { continue }
                $lum = 0.299*$p.R + 0.587*$p.G + 0.114*$p.B
                if ($lum -gt 200 -or $lum -lt 18) { continue }   # 跳过高光和纯黑
                # 量化到 24 级,避免噪点把统计打散
                $k = "$([int]($p.R/24)),$([int]($p.G/24)),$([int]($p.B/24))"
                if (-not $buckets.ContainsKey($k)) { $buckets[$k] = @{ n = 0; r = 0; g = 0; b = 0 } }
                $buckets[$k].n++; $buckets[$k].r += $p.R; $buckets[$k].g += $p.G; $buckets[$k].b += $p.B
            }
        }
        if ($buckets.Count -eq 0) { return $null }
        $top = ($buckets.GetEnumerator() | Sort-Object { $_.Value.n } -Descending | Select-Object -First 1).Value
        return [System.Drawing.Color]::FromArgb(255,
            [int]($top.r / $top.n), [int]($top.g / $top.n), [int]($top.b / $top.n))
    } finally { $img.Dispose() }
}

$target = $null
if ($HairHex) {
    $h = $HairHex.TrimStart('#')
    $target = [System.Drawing.Color]::FromArgb(255,
        [Convert]::ToInt32($h.Substring(0,2),16),
        [Convert]::ToInt32($h.Substring(2,2),16),
        [Convert]::ToInt32($h.Substring(4,2),16))
    Write-Host "发色(手动指定): #$h" -ForegroundColor Cyan
}
elseif ($Ref -and (Test-Path $Ref)) {
    $target = DominantHairColor $Ref
    if ($target) {
        Write-Host ("发色(取自参考图): #{0:X2}{1:X2}{2:X2}" -f $target.R, $target.G, $target.B) -ForegroundColor Cyan
    }
}
if (-not $target) {
    Write-Error "没有发色可用。请给 -HairHex 或一张能取色的 -Ref 图片。"
    exit 1
}

$dstMid  = $target
$dstDark = Shift $target 0.62     # 暗部
$dstHigh = Shift $target 1.28     # 反光

# ---------- 批量换色 ----------
if (-not $SkipFrames) {
    $frames = Get-ChildItem $srcRoot -Recurse -Filter "*.png"
    Write-Host "开始换色,共 $($frames.Count) 帧…" -ForegroundColor Cyan
    $done = 0
    foreach ($f in $frames) {
        $bmp = [System.Drawing.Bitmap]::new($f.FullName)
        $out = [System.Drawing.Bitmap]::new($bmp.Width, $bmp.Height)
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            for ($x = 0; $x -lt $bmp.Width; $x++) {
                $p = $bmp.GetPixel($x, $y)
                if ($p.A -eq 0) { continue }
                # 归到最近的一档原始发色,再换成对应的新档;
                # 差得太远的(比如发饰、皮肤)原样保留
                $dm = ColorDist $p $srcMid; $dd = ColorDist $p $srcDark; $dh = ColorDist $p $srcHigh
                $best = [Math]::Min($dm, [Math]::Min($dd, $dh))
                if ($best -gt 2200) { $out.SetPixel($x, $y, $p); continue }
                if ($best -eq $dm)      { $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $dstMid.R,  $dstMid.G,  $dstMid.B)) }
                elseif ($best -eq $dd)  { $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $dstDark.R, $dstDark.G, $dstDark.B)) }
                else                    { $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($p.A, $dstHigh.R, $dstHigh.G, $dstHigh.B)) }
            }
        }
        $out.Save((Join-Path $texOut $f.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose(); $out.Dispose()
        $done++
        if ($done % 40 -eq 0) { Write-Host "  已处理 $done/$($frames.Count)" }
    }
    Write-Host "换色完成: $done 帧 → $texOut" -ForegroundColor Green
}

# ---------- 头像 ----------
if ($Ref -and (Test-Path $Ref)) {
    # 游戏的角色头像是 icon_character_player,实测 28x28 左右;
    # 这里按最近邻缩放成像素风,保留脸部区域(上半身居中裁一个正方形)
    $src = [System.Drawing.Bitmap]::new($Ref)
    try {
        $side = [Math]::Min($src.Width, $src.Height)
        $cropX = [int](($src.Width - $side) / 2)
        $cropY = [int]($src.Height * 0.06)          # 略微上移,把脸放进画面中心
        if ($cropY + $side -gt $src.Height) { $cropY = $src.Height - $side }
        $rect = New-Object System.Drawing.Rectangle($cropX, $cropY, $side, $side)

        $icon = [System.Drawing.Bitmap]::new(28, 28)
        $g = [System.Drawing.Graphics]::FromImage($icon)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.DrawImage($src, (New-Object System.Drawing.Rectangle(0,0,28,28)), $rect, [System.Drawing.GraphicsUnit]::Pixel)
        $g.Dispose()
        $icon.Save((Join-Path $texOut "icon_character_player.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $icon.Dispose()
        Write-Host "头像已生成: icon_character_player.png (28x28)" -ForegroundColor Green
    } finally { $src.Dispose() }
}

Write-Host ""
Write-Host "下一步: 在仓库根目录跑 .\deploy.ps1 lan-character,然后进游戏 Mod 菜单启用。" -ForegroundColor Yellow
