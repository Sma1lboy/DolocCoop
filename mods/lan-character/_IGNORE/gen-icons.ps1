# 生成 Mod 菜单要的 icon.png(32x32)和 preview.png(512x512)。
#
# 素材来自本 Mod 已生成的头发帧 + 官方身体帧叠合 —— 也就是玩家实际会看到的样子,
# 而不是另画一张。规格对齐现有 mod(red-beret / doloc-coop)。
param([string]$OutRoot = "")

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$modRoot = if ($OutRoot) { $OutRoot } else { Split-Path $PSScriptRoot -Parent }
$hairDir = Join-Path $modRoot "Content\Texture"
$bodyRoot = "C:\Program Files (x86)\Steam\steamapps\workshop\content\2285550\3705665433\Content\04 其他内容示例 Miscellaneous\03 备用贴图 Image Assets\02 主角动画 Character Animations\_IGNORE\02 身体动画 Body Animations"

function Compose([string]$action, [int]$n) {
    # 身体在下、头发在上,和游戏里的图层顺序一致
    $frame = [System.Drawing.Bitmap]::new(64,64)
    $g = [System.Drawing.Graphics]::FromImage($frame)
    $body = Join-Path $bodyRoot "$action\anim_player_body_${action}_${n}.png"
    if (Test-Path $body) { $b=[System.Drawing.Bitmap]::new($body); $g.DrawImage($b,0,0); $b.Dispose() }
    $hair = Join-Path $hairDir "anim_player_hair_${action}_${n}.png"
    if (Test-Path $hair) { $h=[System.Drawing.Bitmap]::new($hair); $g.DrawImage($h,0,0); $h.Dispose() }
    $g.Dispose()
    return $frame
}

function ContentBox($bmp) {
    $x0=$bmp.Width;$y0=$bmp.Height;$x1=-1;$y1=-1
    for ($y=0;$y -lt $bmp.Height;$y++){ for ($x=0;$x -lt $bmp.Width;$x++){
        if ($bmp.GetPixel($x,$y).A -gt 0) {
            if ($x -lt $x0){$x0=$x}; if ($x -gt $x1){$x1=$x}
            if ($y -lt $y0){$y0=$y}; if ($y -gt $y1){$y1=$y} } } }
    return @($x0,$y0,($x1-$x0+1),($y1-$y0+1))
}

# ---------- icon.png 32x32 ----------
# 只取头部:32x32 里塞整个身子会小到看不清是谁,而这个 Mod 改的就是头发
$idle = Compose "idle" 0
$bb = ContentBox $idle
$headH = [int]($bb[3] * 0.67)   # 上 67% = 整个头连下巴;再少会把脸切在眼睛下方
$icon = [System.Drawing.Bitmap]::new(32,32)
$g = [System.Drawing.Graphics]::FromImage($icon)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$side = [Math]::Max($bb[2], $headH)
$scale = [Math]::Floor(30.0 / $side)   # 整数倍缩放,像素才不会被拉歪
if ($scale -lt 1) { $scale = 1 }
$nw = [int]($bb[2]*$scale); $nh = [int]($headH*$scale)
$g.DrawImage($idle, ([System.Drawing.Rectangle]::new([int]((32-$nw)/2), [int]((32-$nh)/2), $nw, $nh)),
             ([System.Drawing.Rectangle]::new($bb[0], $bb[1], $bb[2], $headH)), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$icon.Save((Join-Path $modRoot "icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$icon.Dispose()
Write-Host "icon.png 32x32 (头部裁切, ${scale}x)" -ForegroundColor Green

# ---------- preview.png 512x512 ----------
$poses = @(@("idle",0), @("walk",2), @("run",3), @("water",4))
$prev = [System.Drawing.Bitmap]::new(512,512)
$g = [System.Drawing.Graphics]::FromImage($prev)
# 柔和的草地绿背景,和游戏农场场景同一个色系
$bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
    [System.Drawing.Rectangle]::new(0,0,512,512),
    [System.Drawing.Color]::FromArgb(255,168,204,140),
    [System.Drawing.Color]::FromArgb(255,126,170,110), 90.0)
$g.FillRectangle($bgBrush, 0, 0, 512, 512)
$bgBrush.Dispose()
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

# 直接画 64x64 整帧会溢出画布(64*6=384,两列就 768 了),
# 所以先裁到角色实际占的那 ~22x30,再按能放下的整数倍排布。
# 包围盒对 4 个姿势取并集 —— 各自裁的话每个姿势缩放比不同,大小会跳。
$frames = @(); $uX0=64;$uY0=64;$uX1=-1;$uY1=-1
foreach ($p in $poses) {
    $f = Compose $p[0] $p[1]
    $b = ContentBox $f
    if ($b[0] -lt $uX0){$uX0=$b[0]}; if ($b[1] -lt $uY0){$uY0=$b[1]}
    if (($b[0]+$b[2]-1) -gt $uX1){$uX1=$b[0]+$b[2]-1}
    if (($b[1]+$b[3]-1) -gt $uY1){$uY1=$b[1]+$b[3]-1}
    $frames += $f
}
$bw = $uX1-$uX0+1; $bh = $uY1-$uY0+1
# 注意:整除一律用 [Math]::Floor —— PowerShell 的 [int](3/2) 会得到 2 而不是 1
$cols = 2; $rows = 2; $gap = 24; $topPad = 116; $botPad = 24
$zx = [Math]::Floor((512 - $gap*($cols+1)) / ($cols*$bw))
$zy = [Math]::Floor((512 - $topPad - $botPad - $gap) / ($rows*$bh))
$Z = [Math]::Max(1, [Math]::Min($zx, $zy))
$cw = $bw*$Z; $chh = $bh*$Z
$totalW = $cols*$cw + $gap*($cols-1)
$x0 = [int]((512 - $totalW)/2)
$src = [System.Drawing.Rectangle]::new($uX0, $uY0, $bw, $bh)
for ($i=0; $i -lt $frames.Count; $i++) {
    $x = $x0 + ($i % $cols) * ($cw + $gap)
    $y = $topPad + [Math]::Floor($i / $cols) * ($chh + $gap)
    $g.DrawImage($frames[$i], ([System.Drawing.Rectangle]::new($x, $y, $cw, $chh)), $src, [System.Drawing.GraphicsUnit]::Pixel)
    $frames[$i].Dispose()
}
$title = [System.Drawing.Font]::new("Microsoft YaHei", 34, [System.Drawing.FontStyle]::Bold)
$sub   = [System.Drawing.Font]::new("Microsoft YaHei", 16)
$shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(90,0,0,0))
$white  = [System.Drawing.Brushes]::White
$g.DrawString("小蓝 Lan", $title, $shadow, 26, 26)
$g.DrawString("小蓝 Lan", $title, $white,  24, 24)
$g.DrawString("主角外观替换", $sub, $shadow, 27, 75)
$g.DrawString("主角外观替换", $sub, $white,  25, 73)
$title.Dispose(); $sub.Dispose(); $shadow.Dispose()
$g.Dispose()
$prev.Save((Join-Path $modRoot "preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$prev.Dispose()
Write-Host "preview.png 512x512 (4 个动作, ${Z}x)" -ForegroundColor Green


