# 把生成的大图做成游戏用的 75x58 头像:抠洋红幕布 → 按脸裁 75:58 → 降采样 → 量化到 13 色。
param([string]$Src, [string]$Out, [int]$Colors = 13, [double]$TopFrac = 0.15)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

# GDI+ 的 Save 走 .NET 当前目录,和 PowerShell 的位置不是一回事,相对路径会写不出去
$Out = [System.IO.Path]::GetFullPath($Out, (Get-Location).Path)   # 双参重载:绝对路径原样返回,相对路径才拼
$img = [System.Drawing.Bitmap]::new((Resolve-Path $Src).Path)
$W = $img.Width; $H = $img.Height

# ---- 抠幕布:洋红是 R高 B高 G低,判据比"和四角颜色接近"稳,
#      因为边缘抗锯齿会产生一圈偏暗的洋红,固定容差抠不干净 ----
function IsKey($c) { return ($c.R -gt 110 -and $c.B -gt 110 -and $c.G -lt ($c.R * 0.62) -and $c.G -lt ($c.B * 0.62)) }

# 人物包围盒(顺带用来定位脸)
$x0=$W;$x1=-1;$y0=$H;$y1=-1
for ($y=0;$y -lt $H;$y++){ for ($x=0;$x -lt $W;$x++){
    if (-not (IsKey $img.GetPixel($x,$y))) {
        if ($x -lt $x0){$x0=$x}; if ($x -gt $x1){$x1=$x}
        if ($y -lt $y0){$y0=$y}; if ($y -gt $y1){$y1=$y} } } }
Write-Host ("人物范围 x:{0}-{1} y:{2}-{3}" -f $x0,$x1,$y0,$y1)

# ---- 裁 75:58 ----
# 官方头像是极紧的框:额头到下巴,头发出血到左右边缘。
# 这里以人物横向中心为准,纵向从人物顶端稍往下一点开始 —— 顶端通常是头发,
# 从那儿起裁会把脸推到画面下半部,和官方的构图不一样。
# 用整幅宽度:官方那张头发就是出血到左右两边的。
# 纵向不能以人物包围盒顶端为基准 —— 头发一直顶到画面上沿,y0 恒为 0,
# 框会被顶上去把嘴和下巴切掉。改成按画面高度的固定比例下移。
$AR = 75.0 / 58.0
$cw = $W
$ch = [int]($cw / $AR)
$cx = 0
$cy = [int]($H * $TopFrac)
if ($cx -lt 0) { $cx = 0 }; if ($cy -lt 0) { $cy = 0 }
if ($cx + $cw -gt $W) { $cw = $W - $cx }
if ($cy + $ch -gt $H) { $ch = $H - $cy }

# ---- 降采样。先高质量缩小再量化:直接最近邻缩 1024→75 会把细节抽成噪点 ----
$icon = [System.Drawing.Bitmap]::new(75, 58)
$g = [System.Drawing.Graphics]::FromImage($icon)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($img, ([System.Drawing.Rectangle]::new(0,0,75,58)),
             ([System.Drawing.Rectangle]::new($cx,$cy,$cw,$ch)), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()

# 缩完再抠一次:边缘混了幕布色的像素这时才现形
for ($y=0;$y -lt 58;$y++){ for ($x=0;$x -lt 75;$x++){
    $c = $icon.GetPixel($x,$y)
    if (IsKey $c) { $icon.SetPixel($x,$y,[System.Drawing.Color]::FromArgb(0,0,0,0)) } } }

# ---- 量化到 N 色 ----
$buckets = @{}
for ($y=0;$y -lt 58;$y++){ for ($x=0;$x -lt 75;$x++){
    $c=$icon.GetPixel($x,$y); if ($c.A -eq 0) { continue }
    $k="$([int]($c.R/16)),$([int]($c.G/16)),$([int]($c.B/16))"
    if (-not $buckets.ContainsKey($k)) { $buckets[$k]=@{n=0;r=0;g=0;b=0} }
    $buckets[$k].n++; $buckets[$k].r+=$c.R; $buckets[$k].g+=$c.G; $buckets[$k].b+=$c.B } }
$pal = $buckets.GetEnumerator() | Sort-Object { $_.Value.n } -Descending | Select-Object -First $Colors |
    ForEach-Object { [System.Drawing.Color]::FromArgb(255,[int]($_.Value.r/$_.Value.n),[int]($_.Value.g/$_.Value.n),[int]($_.Value.b/$_.Value.n)) }
for ($y=0;$y -lt 58;$y++){ for ($x=0;$x -lt 75;$x++){
    $c=$icon.GetPixel($x,$y); if ($c.A -eq 0) { continue }
    $best=$null;$bd=[int]::MaxValue
    foreach ($p in $pal) { $d=($c.R-$p.R)*($c.R-$p.R)+($c.G-$p.G)*($c.G-$p.G)+($c.B-$p.B)*($c.B-$p.B); if ($d -lt $bd){$bd=$d;$best=$p} }
    # A 保留原值:半透明的边缘像素直接置实会长出锯齿硬边
    $icon.SetPixel($x,$y,[System.Drawing.Color]::FromArgb($c.A,$best.R,$best.G,$best.B)) } }

$icon.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$icon.Dispose(); $img.Dispose()
Write-Host "→ $Out (75x58, $($pal.Count) 色)" -ForegroundColor Green



