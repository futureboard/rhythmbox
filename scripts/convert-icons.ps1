# Converts a curated set of Lucide SVG icons into an Avalonia ResourceDictionary of
# StreamGeometry resources (Icons.axaml), so they can be used as crisp vector Path icons
# without needing an SVG rendering dependency.

$ErrorActionPreference = "Stop"

$iconsDir = Join-Path $PSScriptRoot "..\src\Assets\Icons\lucide\icons"
$outFile = Join-Path $PSScriptRoot "..\src\Rythmbox.App\Styles\Icons.axaml"

# Map: resource key -> lucide icon file name (without extension)
$icons = [ordered]@{
    IconPlay             = "play"
    IconSquare           = "square"
    IconChevronLeft      = "chevron-left"
    IconChevronRight     = "chevron-right"
    IconRefreshCw        = "refresh-cw"
    IconSlidersHorizontal = "sliders-horizontal"
    IconPower            = "power"
    IconPlug             = "plug"
    IconFolderOpen       = "folder-open"
    IconX                = "x"
    IconSearch           = "search"
    IconVolume2          = "volume-2"
    IconAudioLines       = "audio-lines"
    IconRepeat           = "repeat"
    IconDrum             = "drum"
}

function Format-Number($n) {
    return ([double]$n).ToString([System.Globalization.CultureInfo]::InvariantCulture)
}

function Convert-Rect($el) {
    $x = [double]$el.x
    $y = [double]$el.y
    $w = [double]$el.width
    $h = [double]$el.height
    $r = 0
    if ($el.rx) { $r = [double]$el.rx } elseif ($el.ry) { $r = [double]$el.ry }

    if ($r -eq 0) {
        return "M$(Format-Number $x),$(Format-Number $y) H$(Format-Number ($x+$w)) V$(Format-Number ($y+$h)) H$(Format-Number $x) Z"
    }

    $x1 = Format-Number ($x + $r)
    $x2 = Format-Number ($x + $w - $r)
    $y1 = Format-Number ($y + $r)
    $y2 = Format-Number ($y + $h - $r)
    $xw = Format-Number ($x + $w)
    $yh = Format-Number ($y + $h)
    $xf = Format-Number $x
    $yf = Format-Number $y
    $rf = Format-Number $r

    return "M$x1,$yf H$x2 A$rf,$rf 0 0 1 $xw,$y1 V$y2 A$rf,$rf 0 0 1 $x2,$yh H$x1 A$rf,$rf 0 0 1 $xf,$y2 V$y1 A$rf,$rf 0 0 1 $x1,$yf Z"
}

function Convert-Circle($el) {
    $cx = [double]$el.cx
    $cy = [double]$el.cy
    $r = [double]$el.r
    $x1 = Format-Number ($cx - $r)
    $x2 = Format-Number ($cx + $r)
    $cyF = Format-Number $cy
    $rf = Format-Number $r
    return "M$x1,$cyF A$rf,$rf 0 1 0 $x2,$cyF A$rf,$rf 0 1 0 $x1,$cyF Z"
}

function Convert-Ellipse($el) {
    $cx = [double]$el.cx
    $cy = [double]$el.cy
    $rx = [double]$el.rx
    $ry = [double]$el.ry
    $x1 = Format-Number ($cx - $rx)
    $x2 = Format-Number ($cx + $rx)
    $cyF = Format-Number $cy
    $rxf = Format-Number $rx
    $ryf = Format-Number $ry
    return "M$x1,$cyF A$rxf,$ryf 0 1 0 $x2,$cyF A$rxf,$ryf 0 1 0 $x1,$cyF Z"
}

function Convert-Line($el) {
    $x1 = Format-Number $el.x1
    $y1 = Format-Number $el.y1
    $x2 = Format-Number $el.x2
    $y2 = Format-Number $el.y2
    return "M$x1,$y1 L$x2,$y2"
}

function Convert-Polyline($el) {
    $points = $el.points.Trim() -split '\s+'
    $sb = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $points.Length; $i++) {
        $cmd = if ($i -eq 0) { "M" } else { " L" }
        [void]$sb.Append("$cmd$($points[$i])")
    }
    return $sb.ToString()
}

$entries = @()

foreach ($key in $icons.Keys) {
    $name = $icons[$key]
    $path = Join-Path $iconsDir "$name.svg"
    if (-not (Test-Path $path)) {
        Write-Warning "Missing icon: $name"
        continue
    }

    [xml]$xml = Get-Content $path -Raw
    $svg = $xml.svg
    $segments = @()

    foreach ($child in $svg.ChildNodes) {
        switch ($child.LocalName) {
            # Keep each <path>'s "d" completely untouched: it has its own independent
            # coordinate space starting at (0,0), and a leading relative "m" also
            # implies that every subsequent implicit-repeat command in that same
            # string stays relative too. Re-casing just the first letter (as a
            # previous version of this script did) breaks that chain and corrupts
            # the shape. Instead, each segment below is emitted as its own
            # <StreamGeometry>, which always parses starting fresh at (0,0), so no
            # rewriting is needed at all.
            "path"     { $segments += $child.d.Trim() }
            "rect"     { $segments += (Convert-Rect $child) }
            "circle"   { $segments += (Convert-Circle $child) }
            "ellipse"  { $segments += (Convert-Ellipse $child) }
            "line"     { $segments += (Convert-Line $child) }
            "polyline" { $segments += (Convert-Polyline $child) }
            default    { }
        }
    }

    if ($segments.Count -eq 1) {
        $entries += "    <StreamGeometry x:Key=`"$key`">$($segments[0])</StreamGeometry>"
    }
    else {
        # Multiple independent sub-paths: a GeometryGroup lets each child
        # StreamGeometry parse its "d" in its own fresh coordinate space, instead
        # of concatenating strings into one Path.Data (which would incorrectly
        # carry the "current point" over between unrelated sub-paths).
        $lines = @("    <GeometryGroup x:Key=`"$key`">")
        foreach ($segment in $segments) {
            $lines += "        <StreamGeometry>$segment</StreamGeometry>"
        }
        $lines += "    </GeometryGroup>"
        $entries += ($lines -join "`n")
    }
}

$header = @"
<!--
  Auto-generated from Lucide icons (ISC License) by scripts/convert-icons.ps1.
  Each entry is a 24x24 viewBox stroke-based icon geometry; render with:
  <Path Data="{StaticResource IconName}" Stroke="{DynamicResource RythmboxTextBrush}"
        StrokeThickness="2" StrokeLineCap="Round" StrokeLineJoin="Round" Fill="Transparent" />
-->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
"@

$footer = "</ResourceDictionary>"

$content = $header + "`n" + ($entries -join "`n") + "`n" + $footer
Set-Content -Path $outFile -Value $content -Encoding UTF8

Write-Output "Wrote $($icons.Count) icons to $outFile"
