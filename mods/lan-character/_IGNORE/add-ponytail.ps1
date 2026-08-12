# 给已换色的头发帧加一条低马尾。
#
#   .\add-ponytail.ps1                    就地改 Content\Texture
#   .\add-ponytail.ps1 -Preview           只出对比图,不动文件
#
# 为什么能做:
#   实测 144 帧里眼睛横宽只有 0 和 6 两种值,没有中间态 —— 角色永远正面朝向,
#   左右转身是游戏运行时整体镜像。所以不需要逐帧推断朝向,马尾固定挂一侧即可,
#   转身时它会跟着一起翻,这在这种画风里是正常处理。
#
#   锚点取自每帧头发自己的包围盒,所以马尾自动跟着脑袋走 —— 走路的起伏、
#   跑步时头的前倾,都不用单独处理。
#
#   只往透明像素上画,绝不覆盖原有发丝 —— 万一锚点算歪了,最坏结果是马尾长歪,
#   而不是把头发啃掉一块。
param(
    [string]$TexDir = "",
    [string]$Side = "left",          # 挂哪一侧。left = 焦糖那缕所在的一侧
    [double]$AnchorFrac = 0.58,      # 附着点在头发包围盒高度的百分之几处
    [int]$Length = 12,
    [switch]$Preview,
    [string]$PreviewOut = ""
)

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

$modRoot = Split-Path $PSScriptRoot -Parent
if (-not $TexDir) { $TexDir = Join-Path $modRoot "Content\Texture" }

# 三档发色,和 gen-character.ps1 保持一致
$MID  = [System.Drawing.Color]::FromArgb(255,0x24,0x1F,0x26)
$DARK = [System.Drawing.Color]::FromArgb(255,0x14,0x11,0x15)
$HI   = [System.Drawing.Color]::FromArgb(255,0xA8,0x7B,0x4E)

# 马尾模板。行 = 从附着点往下;4 列,最右列紧贴头部边缘。
# '#'=暗部  'o'=主色  'H'=焦糖  '.'=不画
#
# 必须 4 列宽:3 列的版本里最内侧那列落在头发的不透明像素上,
# 被"只画透明处"的保护规则跳过,实到只有 2 像素 —— 看起来像根刺不像头发。
# 现在整个模板都排在头外侧,不和头发抢像素。
$TEMPLATE = @(
    ".oo.",
    "#ooo",
    "#ooo",
    "#oHo",
    "#oHo",
    "#oHo",
    "#ooo",
    "#ooo",
    ".#oo",
    "..##"
)

# 这些姿势不加:身体是躺倒/骑乘/攀爬的,竖直下垂的马尾会穿模。
# stand 是"从床上起身"的整套动画,前几帧人是躺着的,一并跳过。
$SKIP = @("faint","knock","hit","ride_idle","ride_run","climb","climb_jump","sit","stand")

function CharToColor($ch) {
    switch ($ch) { '#' { $DARK } 'o' { $MID } 'H' { $HI } default { $null } }
}

# GDI+ 从路径构造 Bitmap 会一直锁着那个文件,想原地改写就会撞上
# "A generic error occurred in GDI+"。先整个读进内存流,文件就不被占用了。
# 注意流不能提前释放 —— Bitmap 在其整个生命周期里都依赖它。
$script:openStreams = @()
function LoadBitmap([string]$path) {
    $ms = [System.IO.MemoryStream]::new([System.IO.File]::ReadAllBytes($path))
    $script:openStreams += $ms
    return [System.Drawing.Bitmap]::new($ms)
}

function HairBox($bmp) {
    $x0=$bmp.Width;$y0=$bmp.Height;$x1=-1;$y1=-1
    for ($y=0;$y -lt $bmp.Height;$y++){ for ($x=0;$x -lt $bmp.Width;$x++){
        if ($bmp.GetPixel($x,$y).A -gt 0) {
            if ($x -lt $x0){$x0=$x}; if ($x -gt $x1){$x1=$x}
            if ($y -lt $y0){$y0=$y}; if ($y -gt $y1){$y1=$y} } } }
    if ($x1 -lt 0) { return $null }
    return @{ x0=$x0; y0=$y0; x1=$x1; y1=$y1; w=($x1-$x0+1); h=($y1-$y0+1) }
}

function AddTail($bmp) {
    $b = HairBox $bmp
    if (-not $b) { return $false }

    # 不另贴一条尾巴,而是把已有的那一缕侧发往下接长。
    #
    # 先前贴模板的做法总是和脑袋之间空一列:锚点用的是包围盒边缘,
    # 而那个值来自最宽的那几行,在附着行上头发其实没伸到那么外面。
    # 改成逐列从"该列最底端的实际像素"往下长,缝隙在原理上就不存在了。
    #
    # 颜色也逐列继承该列底端像素的颜色 —— 原本那缕是 暗/焦糖/焦糖/暗,
    # 接出来的部分自动保持同样的明暗和挑染,不用另配色。
    $lockW = [Math]::Max(3, [Math]::Floor($b.w * 0.22))
    $cols = @()
    if ($Side -eq "left") { $cols = $b.x0..([Math]::Min($b.x1, $b.x0 + $lockW - 1)) }
    else { $cols = ([Math]::Max($b.x0, $b.x1 - $lockW + 1))..$b.x1 }

    # 每列的底端像素(位置 + 颜色)
    $base = @{}
    foreach ($x in $cols) {
        for ($y = $b.y1; $y -ge $b.y0; $y--) {
            $p = $bmp.GetPixel($x, $y)
            if ($p.A -ne 0) { $base[$x] = @{ y = $y; c = $p }; break }
        }
    }
    if ($base.Count -eq 0) { return $false }

    $painted = 0
    for ($r = 1; $r -le $Length; $r++) {
        # 越往下越收窄:先掉最外侧一列,再掉一列,末端收成一两像素
        $shrinkOuter = [Math]::Floor($r * 2.0 / $Length)
        $shrinkInner = [Math]::Floor($r * 1.0 / $Length)
        foreach ($x in $base.Keys) {
            $fromOuter = if ($Side -eq "left") { $x - $b.x0 } else { $b.x1 - $x }
            $fromInner = ($cols.Count - 1) - $fromOuter
            if ($fromOuter -lt $shrinkOuter) { continue }
            if ($fromInner -lt $shrinkInner) { continue }
            $y = $base[$x].y + $r
            if ($y -ge $bmp.Height) { continue }
            if ($bmp.GetPixel($x, $y).A -ne 0) { continue }   # 原有发丝一根不动
            $bmp.SetPixel($x, $y, $base[$x].c)
            $painted++
        }
    }
    return ($painted -gt 0)
}

$files = Get-ChildItem $TexDir -Filter "anim_player_hair_*.png"
if ($files.Count -eq 0) { Write-Error "没找到头发帧,先跑 gen-character.ps1"; exit 1 }

if (-not $Preview) {
    $done = 0; $skipped = 0
    foreach ($f in $files) {
        $act = $f.BaseName -replace '^anim_player_hair_','' -replace '_\d+$',''
        if ($SKIP -contains $act) { $skipped++; continue }
        $b = LoadBitmap $f.FullName
        $ok = AddTail $b
        if ($ok) { $b.Save($f.FullName, [System.Drawing.Imaging.ImageFormat]::Png); $done++ }
        $b.Dispose()
    }
    Write-Host "马尾已加到 $done 帧,跳过 $skipped 帧(躺倒/骑乘/攀爬姿势)" -ForegroundColor Green
    exit 0
}

# ---------- 预览:身体垫底,加尾前后并排 ----------
$bodyRoot = "C:\Program Files (x86)\Steam\steamapps\workshop\content\2285550\3705665433\Content\04 其他内容示例 Miscellaneous\03 备用贴图 Image Assets\02 主角动画 Character Animations\_IGNORE\02 身体动画 Body Animations"
$shots = @(@("idle",0),@("walk",2),@("run",3),@("stand",0),@("water",4),@("swing",3),@("chop",2),@("interact",1))
$Z = 5; $cellW = 64*$Z
$out = [System.Drawing.Bitmap]::new($cellW*$shots.Count/2, $cellW*4 + 60)
$g = [System.Drawing.Graphics]::FromImage($out)
$g.Clear([System.Drawing.Color]::FromArgb(255,150,185,130))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$font = [System.Drawing.Font]::new("Microsoft YaHei", 12)

for ($i=0; $i -lt $shots.Count; $i++) {
    $act = $shots[$i][0]; $idx = $shots[$i][1]
    $hairPath = Join-Path $TexDir "anim_player_hair_${act}_${idx}.png"
    if (-not (Test-Path $hairPath)) { continue }
    for ($variant=0; $variant -lt 2; $variant++) {
        $frame = [System.Drawing.Bitmap]::new(64,64)
        $fg = [System.Drawing.Graphics]::FromImage($frame)
        $bp = Join-Path $bodyRoot "$act\anim_player_body_${act}_${idx}.png"
        if (Test-Path $bp) { $bb=[System.Drawing.Bitmap]::new($bp); $fg.DrawImage($bb,0,0); $bb.Dispose() }
        $h = LoadBitmap $hairPath
        if ($variant -eq 1 -and $SKIP -notcontains $act) { [void](AddTail $h) }
        $fg.DrawImage($h,0,0); $h.Dispose(); $fg.Dispose()
        $col = $i % 4; $row = [Math]::Floor($i / 4) * 2 + $variant
        $g.DrawImage($frame, $col*$cellW, 30 + $row*$cellW, $cellW, $cellW)
        $frame.Dispose()
        if ($col -eq 0) {
            $g.DrawString($(if($variant -eq 0){"原"}else{"马尾"}), $font,
                [System.Drawing.Brushes]::White, 2, 30 + $row*$cellW + 4)
        }
    }
    $g.DrawString($act, $font, [System.Drawing.Brushes]::White, ($i%4)*$cellW + 40, 6 + [Math]::Floor($i/4)*($cellW*2))
}
$g.Dispose()
$p = if ($PreviewOut) { $PreviewOut } else { Join-Path $PSScriptRoot "ponytail-preview.png" }
$out.Save($p, [System.Drawing.Imaging.ImageFormat]::Png); $out.Dispose()
Write-Host "预览图: $p" -ForegroundColor Green


