# 生成 DolocCoop 的 icon.png 与 preview.png(两个玩家小人 + 连接线)
Add-Type -AssemblyName System.Drawing
$mod = Split-Path $PSScriptRoot -Parent

$cBlue   = [System.Drawing.Color]::FromArgb(255, 74, 144, 226)   # 玩家 A
$cRed    = [System.Drawing.Color]::FromArgb(255, 201, 62, 62)    # 玩家 B
$cLink   = [System.Drawing.Color]::FromArgb(255, 120, 220, 150)  # 连接线
$cInk    = [System.Drawing.Color]::FromArgb(255, 32, 24, 40)
$cBg     = [System.Drawing.Color]::FromArgb(255, 46, 36, 56)

# 7x9 小人字符画: .=透明 O=描边 C=主色 F=脸
$person = @(
  "..OOO..",
  ".OCCCO.",
  ".OFFFO.",
  "OOCCCOO",
  "OCCCCCO",
  "OCCCCCO",
  ".OCCCO.",
  "..O.O..",
  "..O.O.."
)

function Draw-Person($g, $ox, $oy, $scale, $main) {
  $brushMain = New-Object System.Drawing.SolidBrush($main)
  $brushInk  = New-Object System.Drawing.SolidBrush($cInk)
  $brushFace = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 218, 186))
  for ($y = 0; $y -lt $person.Count; $y++) {
    for ($x = 0; $x -lt $person[$y].Length; $x++) {
      $c = $person[$y][$x]
      if ($c -eq '.') { continue }
      $b = switch ($c) { 'O' { $brushInk } 'C' { $brushMain } 'F' { $brushFace } }
      $g.FillRectangle($b, $ox + $x * $scale, $oy + $y * $scale, $scale, $scale)
    }
  }
  $brushMain.Dispose(); $brushInk.Dispose(); $brushFace.Dispose()
}

function New-Branding([string]$path, [int]$size, [bool]$withText) {
  $bmp = New-Object System.Drawing.Bitmap($size, $size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.Clear($cBg)

  $scale = [int]($size / 24)
  if ($scale -lt 1) { $scale = 1 }
  $pw = 7 * $scale
  $gap = [int]($size / 6)
  $totalW = $pw * 2 + $gap
  $ox = [int](($size - $totalW) / 2)
  $oy = if ($withText) { [int]($size * 0.22) } else { [int](($size - 9 * $scale) / 2) }

  # 连接线(两人之间的网络链路)
  $linkBrush = New-Object System.Drawing.SolidBrush($cLink)
  $linkY = $oy + 4 * $scale
  $dot = [int]([Math]::Max(1, $scale * 0.6))
  for ($x = $ox + $pw; $x -lt $ox + $pw + $gap; $x += $dot * 2) {
    $g.FillRectangle($linkBrush, $x, $linkY, $dot, $dot)
  }
  $linkBrush.Dispose()

  Draw-Person $g $ox $oy $scale $cBlue
  Draw-Person $g ($ox + $pw + $gap) $oy $scale $cRed

  if ($withText) {
    $font = New-Object System.Drawing.Font("Microsoft YaHei", [int]($size / 13), [System.Drawing.FontStyle]::Bold)
    $tb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 233, 224))
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("DolocCoop", $font, $tb, $size / 2, $size * 0.62, $fmt)
    $font2 = New-Object System.Drawing.Font("Microsoft YaHei", [int]($size / 18))
    $tb2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 170, 200, 230))
    $g.DrawString("Steam 联机 · 一起经营小镇", $font2, $tb2, $size / 2, $size * 0.78, $fmt)
    $font.Dispose(); $font2.Dispose(); $tb.Dispose(); $tb2.Dispose()
  }

  $g.Dispose()
  $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host "生成 $path"
}

New-Branding (Join-Path $mod "icon.png")    32  $false
New-Branding (Join-Path $mod "preview.png") 512 $true
