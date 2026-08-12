# 生成红色贝雷帽全部贴图(像素图由字符画定义,重跑即可重新生成)
Add-Type -AssemblyName System.Drawing
$mod = Split-Path $PSScriptRoot -Parent
$tex = Join-Path $mod "Content\Texture"
New-Item -ItemType Directory -Force $tex | Out-Null

# 18x9 贝雷帽字符画: .=透明 O=描边 R=红 H=高光 D=暗红帽檐 S=帽蒂
$map = @(
  "........OO........",
  ".......OSSO.......",
  ".....OORRRROO.....",
  "....ORRRRRRRRO....",
  "...ORHHRRRRRRRO...",
  "..ORHHRRRRRRRRRO..",
  ".ORRRRRRRRRRRRRRO.",
  ".ODDDDDDDDDDDDDDO.",
  "..OOOOOOOOOOOOOO.."
)
$colors = @{
  'O' = [System.Drawing.Color]::FromArgb(255, 58, 20, 32)
  'R' = [System.Drawing.Color]::FromArgb(255, 201, 48, 56)
  'H' = [System.Drawing.Color]::FromArgb(255, 232, 100, 106)
  'D' = [System.Drawing.Color]::FromArgb(255, 142, 32, 38)
  'S' = [System.Drawing.Color]::FromArgb(255, 110, 30, 36)
}

function New-BeretPng([string]$path, [int]$w, [int]$h, [int]$ox, [int]$oy) {
  $bmp = New-Object System.Drawing.Bitmap($w, $h)
  for ($y = 0; $y -lt $map.Count; $y++) {
    for ($x = 0; $x -lt $map[$y].Length; $x++) {
      $c = $map[$y][$x]
      if ($c -ne '.') { $bmp.SetPixel($ox + $x, $oy + $y, $colors["$c"]) }
    }
  }
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "生成 $path"
}

# 帽子锚点: idle 头顶 y=22 x=22..39 / climb 头顶 y=23 x=23..39 (由示例模组主角贴图扫描得出)
New-BeretPng (Join-Path $tex "anim_hat_jax_beret_idle_0.png")  64 64 22 17
New-BeretPng (Join-Path $tex "anim_hat_jax_beret_climb_0.png") 64 64 23 18
New-BeretPng (Join-Path $tex "preview_hat_jax_beret.png")      64 64 23 28
New-BeretPng (Join-Path $tex "icon_item_jax_beret.png")        28 28  5  9
New-BeretPng (Join-Path $mod "icon.png")                       32 32  7 11

# 创意工坊预览图 512x512: 放大版贝雷帽 + 标题
$bmp = New-Object System.Drawing.Bitmap(512, 512)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 46, 36, 56))
$scale = 20
$ox = [int]((512 - 18 * $scale) / 2); $oy = 110
for ($y = 0; $y -lt $map.Count; $y++) {
  for ($x = 0; $x -lt $map[$y].Length; $x++) {
    $c = $map[$y][$x]
    if ($c -ne '.') {
      $brush = New-Object System.Drawing.SolidBrush($colors["$c"])
      $g.FillRectangle($brush, $ox + $x * $scale, $oy + $y * $scale, $scale, $scale)
      $brush.Dispose()
    }
  }
}
$font = New-Object System.Drawing.Font("Microsoft YaHei", 30, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 230, 220))
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("红色贝雷帽", $font, $textBrush, 256, 340, $fmt)
$g.DrawString("Red Beret", $font, $textBrush, 256, 400, $fmt)
$g.Dispose(); $font.Dispose(); $textBrush.Dispose()
$bmp.Save((Join-Path $mod "preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "生成 preview.png"

# 校验图(仅本地): 主角 idle + 帽子合成,便于检查佩戴位置
$playerPng = "C:\Program Files (x86)\Steam\steamapps\workshop\content\2285550\3705665433\Content\01 美化模组示例 Replacing Existing Content Assets\01 主角 Protagonist\01 本体 Character\03 整体动画  Combined Animations\_IGNORE\idle\anim_player_idle_0.png"
if (Test-Path $playerPng) {
  $base = New-Object System.Drawing.Bitmap($playerPng)
  $hat = New-Object System.Drawing.Bitmap((Join-Path $tex "anim_hat_jax_beret_idle_0.png"))
  $g = [System.Drawing.Graphics]::FromImage($base)
  $g.DrawImage($hat, 0, 0, 64, 64)
  $g.Dispose()
  $big = New-Object System.Drawing.Bitmap(256, 256)
  $g2 = [System.Drawing.Graphics]::FromImage($big)
  $g2.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
  $g2.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
  $g2.DrawImage($base, 0, 0, 256, 256)
  $g2.Dispose()
  $big.Save((Join-Path $PSScriptRoot "wear-check.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $base.Dispose(); $hat.Dispose(); $big.Dispose()
  Write-Host "生成 _IGNORE\wear-check.png (佩戴效果校验图)"
}
