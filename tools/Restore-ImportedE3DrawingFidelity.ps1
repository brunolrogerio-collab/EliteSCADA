param(
    [Parameter(Mandatory = $true)]
    [string] $SourceDirectory,
    [string] $DataFile = (Join-Path $PSScriptRoot '..\src\Scada.Api\Runtime\ImportedE3DynamoLibraryData.json')
)

$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::GetCultureInfo('pt-BR')

function Convert-E3Number([string] $value, [double] $fallback = 0) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $fallback }
    return [double]::Parse($value, [Globalization.NumberStyles]::Float, $culture)
}

function Convert-E3ColorRef([string] $value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '#000000' }
    $color = [long]::Parse($value, [Globalization.CultureInfo]::InvariantCulture)
    $red = $color -band 255
    $green = ($color -shr 8) -band 255
    $blue = ($color -shr 16) -band 255
    return '#{0:X2}{1:X2}{2:X2}' -f $red, $green, $blue
}

function Set-DrawingProperty($target, [string] $name, $value) {
    $target | Add-Member -MemberType NoteProperty -Name $name -Value $value -Force
}

function Get-PolylinePoints($rows, [int] $rowIndex, $drawing) {
    $pointsName = "$($drawing.Name).Points"
    for ($pathIndex = $rowIndex + 1; $pathIndex -lt $rows.Count; $pathIndex++) {
        if ($rows[$pathIndex].ObjectType -eq 'IDrawLinePoints' -and $rows[$pathIndex].Name -eq $pointsName) {
            $points = [System.Collections.Generic.List[object]]::new()
            for ($pointIndex = $pathIndex + 1; $pointIndex -lt $rows.Count -and $rows[$pointIndex].ObjectType -eq 'IDrawLinePoint'; $pointIndex++) {
                $points.Add([pscustomobject]@{
                    X = Convert-E3Number $rows[$pointIndex].X
                    Y = Convert-E3Number $rows[$pointIndex].Y
                })
            }
            return ,$points.ToArray()
        }
    }
    return ,@()
}

function Test-ClosedPolyline($points) {
    if ($points.Count -lt 3) { return $false }
    $first = $points[0]
    $last = $points[-1]
    return [Math]::Abs($first.X - $last.X) -lt 1 -and [Math]::Abs($first.Y - $last.Y) -lt 1
}

$definitions = Get-Content -LiteralPath $DataFile -Raw | ConvertFrom-Json
$updated = [System.Collections.Generic.List[string]]::new()
$skipped = [System.Collections.Generic.List[string]]::new()
    $drawingTypes = @('DrawRect', 'DrawCircle', 'DrawArc', 'DrawBezier', 'DrawTank', 'DrawLine', 'DrawString', 'Label')

foreach ($definition in $definitions) {
    $csvPath = Join-Path $SourceDirectory $definition.sourceFile
    if (-not (Test-Path -LiteralPath $csvPath)) {
        $skipped.Add("$($definition.name): export not found")
        continue
    }

    $rows = @(Import-Csv -LiteralPath $csvPath -Delimiter ';')
    $rootGroup = $rows | Where-Object { $_.ObjectType -eq 'DrawGroup' } | Select-Object -First 1
    $drawings = [System.Collections.Generic.List[object]]::new()
    for ($rowIndex = 0; $rowIndex -lt $rows.Count; $rowIndex++) {
        $row = $rows[$rowIndex]
        if ($row.ObjectType -notin $drawingTypes) { continue }

        $segments = 1
        $originalSegments = 1
        $isPolygon = $false
        if ($row.ObjectType -eq 'DrawLine') {
            $linePoints = Get-PolylinePoints $rows $rowIndex $row
            $isPolygon = (Test-ClosedPolyline $linePoints) -or ((Convert-E3Number $row.PolygonFill) -ne 0)
            $originalSegments = [Math]::Max(1, $linePoints.Count - 1)
            $segments = if ($isPolygon) { 1 } else { $originalSegments }
        }
        $drawings.Add([pscustomobject]@{ Row = $row; Segments = $segments; OriginalSegments = $originalSegments; IsPolygon = $isPolygon; RowIndex = $rowIndex })
    }

    $requiredElements = ($drawings | Measure-Object -Property Segments -Sum).Sum
    $legacyElementCount = ($drawings | Measure-Object -Property OriginalSegments -Sum).Sum
    $bezierCount = @($drawings | Where-Object { $_.Row.ObjectType -eq 'DrawBezier' }).Count
    $sourceIncludesBezier = $definition.elements.Count -in @($requiredElements, $legacyElementCount)
    $sourceUsesLegacySegments = $definition.elements.Count -eq $legacyElementCount
    if (-not $sourceIncludesBezier -and ($definition.elements.Count -notin @(($requiredElements - $bezierCount), ($legacyElementCount - $bezierCount)))) {
        $skipped.Add("$($definition.name): source/drawing element counts differ ($requiredElements/$($definition.elements.Count))")
        continue
    }

    $originalElements = @($definition.elements)
    $restoredElements = [System.Collections.Generic.List[object]]::new()
    $rootWidth = [Math]::Max((Convert-E3Number $rootGroup.Width 1), 1)
    $rootHeight = [Math]::Max((Convert-E3Number $rootGroup.Height 1), 1)
    $scaleX = $definition.width / $rootWidth
    $scaleY = $definition.height / $rootHeight

    # Rebuild the DrawGroup hierarchy from E3 paths. Group geometry is stored
    # in root-dynamo coordinates; child geometry will become parent-relative
    # when the API turns these records into canonical `core.group` elements.
    $sourceGroups = [System.Collections.Generic.List[object]]::new()
    $groupRows = @($rows | Where-Object { $_.ObjectType -eq 'DrawGroup' })
    $rootGroupPath = [string]$rootGroup.PathName
    $groupPathToId = @{}
    $groupNumber = 0
    foreach ($groupRow in $groupRows) {
        $groupNumber++
        $groupPath = [string]$groupRow.PathName
        if ([string]::IsNullOrWhiteSpace($groupPath)) { continue }
        $groupId = 'g{0:D3}' -f $groupNumber
        $parent = $groupRows |
            Where-Object { [string]$_.PathName -ne $groupPath -and ([string]$groupRow.PathName).StartsWith(([string]$_.PathName) + '.', [StringComparison]::Ordinal) } |
            Sort-Object { ([string]$_.PathName).Length } -Descending |
            Select-Object -First 1
        $parentId = if ($parent -and $groupPathToId.ContainsKey([string]$parent.PathName)) { $groupPathToId[[string]$parent.PathName] } else { $null }
        $groupPathToId[$groupPath] = $groupId
        $isRoot = $groupPath -eq $rootGroupPath
        $sourceGroups.Add([pscustomobject][ordered]@{
            id = $groupId
            parentId = $parentId
            x = if ($isRoot) { 0 } else { [Math]::Round(((Convert-E3Number $groupRow.X) - (Convert-E3Number $rootGroup.X)) * $scaleX, 4) }
            y = if ($isRoot) { 0 } else { [Math]::Round(((Convert-E3Number $groupRow.Y) - (Convert-E3Number $rootGroup.Y)) * $scaleY, 4) }
            width = if ($isRoot) { $definition.width } else { [Math]::Round([Math]::Max((Convert-E3Number $groupRow.Width) * $scaleX, 1), 4) }
            height = if ($isRoot) { $definition.height } else { [Math]::Round([Math]::Max((Convert-E3Number $groupRow.Height) * $scaleY, 1), 4) }
        })
    }

    $elementIndex = 0
    foreach ($drawingEntry in $drawings) {
        $drawing = $drawingEntry.Row
        $sourceParentGroup = $groupRows |
            Where-Object { ([string]$drawing.PathName).StartsWith(([string]$_.PathName) + '.', [StringComparison]::Ordinal) } |
            Sort-Object { ([string]$_.PathName).Length } -Descending |
            Select-Object -First 1
        $groupId = if ($sourceParentGroup) { $groupPathToId[[string]$sourceParentGroup.PathName] } else { $null }

        if ($drawingEntry.IsPolygon) {
            $element = $originalElements[$elementIndex]
            $points = Get-PolylinePoints $rows $drawingEntry.RowIndex $drawing
            $minX = ($points | Measure-Object -Property X -Minimum).Minimum
            $minY = ($points | Measure-Object -Property Y -Minimum).Minimum
            $maxX = ($points | Measure-Object -Property X -Maximum).Maximum
            $maxY = ($points | Measure-Object -Property Y -Maximum).Maximum
            $localPoints = @($points | ForEach-Object {
                [pscustomobject][ordered]@{
                    x = [Math]::Round(($_.X - $minX) * $scaleX, 4)
                    y = [Math]::Round(($_.Y - $minY) * $scaleY, 4)
                }
            })
            if ($localPoints.Count -gt 1 -and $localPoints[0].x -eq $localPoints[-1].x -and $localPoints[0].y -eq $localPoints[-1].y) {
                $localPoints = @($localPoints | Select-Object -First ($localPoints.Count - 1))
            }
            $element.type = 'core.polygon'
            Set-DrawingProperty $element 'x' ([Math]::Round(($minX - (Convert-E3Number $rootGroup.X)) * $scaleX, 4))
            Set-DrawingProperty $element 'y' ([Math]::Round(($minY - (Convert-E3Number $rootGroup.Y)) * $scaleY, 4))
            Set-DrawingProperty $element 'width' ([Math]::Round([Math]::Max(($maxX - $minX) * $scaleX, 1), 4))
            Set-DrawingProperty $element 'height' ([Math]::Round([Math]::Max(($maxY - $minY) * $scaleY, 1), 4))
            Set-DrawingProperty $element 'points' $localPoints
            Set-DrawingProperty $element 'zIndex' $restoredElements.Count
            Set-DrawingProperty $element 'groupId' $groupId
            Set-DrawingProperty $element 'strokeColor' (Convert-E3ColorRef $drawing.BorderColor)
            $sourceWidth = Convert-E3Number $drawing.BorderWidth
            Set-DrawingProperty $element 'strokeWidth' $(if ($sourceWidth -gt 0) { [Math]::Max($sourceWidth / 100, 0.5) } else { 0.5 })
            $fillStyleValue = [int](Convert-E3Number $drawing.FillStyle)
            $fillColor = if ($fillStyleValue -eq 11) { Convert-E3ColorRef $drawing.BackgroundColor } else { Convert-E3ColorRef $drawing.ForegroundColor }
            Set-DrawingProperty $element 'polygonFillRule' $(if ((Convert-E3Number $drawing.PolygonFill) -eq 1) { 'nonzero' } else { 'evenodd' })
            if ($fillStyleValue -in @(1, 10)) {
                Set-DrawingProperty $element 'fillStyle' 'none'
                Set-DrawingProperty $element 'fillColor' $fillColor
            } elseif ($fillStyleValue -eq 8) {
                $primary = Convert-E3ColorRef $drawing.ForegroundColor
                $secondary = Convert-E3ColorRef $drawing.BackgroundColor
                $direction = switch ([int](Convert-E3Number $drawing.GradientStyle)) {
                    0 { 'horizontal' }
                    1 { $swap = $primary; $primary = $secondary; $secondary = $swap; 'horizontal' }
                    4 { 'vertical' }
                    5 { $swap = $primary; $primary = $secondary; $secondary = $swap; 'vertical' }
                    8 { 'diagonal-down' }
                    9 { 'diagonal-up' }
                    12 { 'diagonal-down' }
                    13 { 'diagonal-up' }
                    default { 'vertical' }
                }
                Set-DrawingProperty $element 'fillStyle' 'gradient'
                Set-DrawingProperty $element 'fillColor' $primary
                Set-DrawingProperty $element 'fillSecondaryColor' $secondary
                Set-DrawingProperty $element 'gradientDirection' $direction
            } else {
                Set-DrawingProperty $element 'fillStyle' 'solid'
                Set-DrawingProperty $element 'fillColor' $fillColor
            }
            $restoredElements.Add($element)
            $elementIndex += if ($sourceUsesLegacySegments) { $drawingEntry.OriginalSegments } else { 1 }
            continue
        }

        if ($drawing.ObjectType -eq 'DrawBezier') {
            $curve = [pscustomobject][ordered]@{
                x = [Math]::Round(((Convert-E3Number $drawing.X) - (Convert-E3Number $rootGroup.X)) * $scaleX, 4)
                y = [Math]::Round(((Convert-E3Number $drawing.Y) - (Convert-E3Number $rootGroup.Y)) * $scaleY, 4)
                width = [Math]::Round([Math]::Max((Convert-E3Number $drawing.Width) * $scaleX, 1), 4)
                height = [Math]::Round([Math]::Max((Convert-E3Number $drawing.Height) * $scaleY, 1), 4)
                rotation = Convert-E3Number $drawing.Angle
                type = 'core.bezier'
                key = "e3-bezier-$($drawings.IndexOf($drawingEntry) + 1)"
                # CSV rows are in the source drawing order. Store it explicitly
                # so nested E3 groups keep the same front/back relationship after
                # they are flattened into our canvas elements.
                zIndex = $restoredElements.Count
                groupId = $groupId
                strokeColor = Convert-E3ColorRef $drawing.BorderColor
                strokeWidth = [Math]::Max((Convert-E3Number $drawing.BorderWidth) / 100, 0.5)
                fillStyle = if ((Convert-E3Number $drawing.FillStyle) -in @(1, 10)) { 'none' } else { 'solid' }
                fillColor = Convert-E3ColorRef $drawing.ForegroundColor
                bezierPath = 'M 0 20 C 20 0 80 0 100 20 L 100 100 L 0 100 Z'
            }
            $restoredElements.Add($curve)
            if ($sourceIncludesBezier) { $elementIndex++ }
            continue
        }

        for ($segmentIndex = 0; $segmentIndex -lt $drawingEntry.Segments; $segmentIndex++) {
            $element = $originalElements[$elementIndex + $segmentIndex]
            $properties = $element
            Set-DrawingProperty $properties 'zIndex' $restoredElements.Count
            Set-DrawingProperty $properties 'groupId' $groupId

            if ($drawing.ObjectType -eq 'DrawArc') {
                $element.type = 'core.arc'
                Set-DrawingProperty $properties 'arcStartAngle' (Convert-E3Number $drawing.ArcBeginAngle)
                Set-DrawingProperty $properties 'arcEndAngle' (Convert-E3Number $drawing.ArcEndAngle 90)
                $arcStyle = switch ([int](Convert-E3Number $drawing.ArcStyle 2)) {
                    0 { 'arc' }
                    1 { 'chord' }
                    default { 'pie' }
                }
                Set-DrawingProperty $properties 'arcStyle' $arcStyle
            }

            if ($drawing.ObjectType -in @('DrawRect', 'DrawCircle', 'DrawArc', 'DrawBezier', 'DrawTank', 'DrawLine')) {
                # In E3 CSV, BorderColor is the visible outline. BorderWidth is
                # exported in hundredths; leaving it raw can turn a 0.5px line
                # into a 50px opaque band that covers neighboring grouped shapes.
                Set-DrawingProperty $properties 'strokeColor' (Convert-E3ColorRef $drawing.BorderColor)
                $sourceWidth = Convert-E3Number $drawing.BorderWidth
                Set-DrawingProperty $properties 'strokeWidth' $(if ($sourceWidth -gt 0) { [Math]::Max($sourceWidth / 100, 0.5) } else { 0.5 })
            }

            # E3 FillStyle determines whether ForegroundColor, BackgroundColor,
            # or both are visible. Apply it to every filled shape, not just
            # animation layers; generic fallback colors can create false yellow
            # or blue blocks behind unrelated members of a DrawGroup.
            if ($drawing.ObjectType -in @('DrawRect', 'DrawCircle', 'DrawArc', 'DrawBezier', 'DrawTank')) {
                $fillStyleValue = [int](Convert-E3Number $drawing.FillStyle)
                $sourceFill = if ($fillStyleValue -eq 11) {
                    Convert-E3ColorRef $drawing.BackgroundColor
                } else {
                    Convert-E3ColorRef $drawing.ForegroundColor
                }
                if ($fillStyleValue -in @(1, 10)) {
                    Set-DrawingProperty $properties 'fillStyle' 'none'
                    Set-DrawingProperty $properties 'fillColor' $sourceFill
                } elseif ($fillStyleValue -eq 8) {
                $primary = Convert-E3ColorRef $drawing.ForegroundColor
                $secondary = Convert-E3ColorRef $drawing.BackgroundColor
                $direction = switch ([int](Convert-E3Number $drawing.GradientStyle)) {
                    0 { 'horizontal' }
                    1 { $swap = $primary; $primary = $secondary; $secondary = $swap; 'horizontal' }
                    4 { 'vertical' }
                    5 { $swap = $primary; $primary = $secondary; $secondary = $swap; 'vertical' }
                    8 { 'diagonal-down' }
                    9 { 'diagonal-up' }
                    12 { 'diagonal-down' }
                    13 { 'diagonal-up' }
                    default { 'vertical' }
                }
                Set-DrawingProperty $properties 'fillStyle' 'gradient'
                Set-DrawingProperty $properties 'fillColor' $primary
                Set-DrawingProperty $properties 'fillSecondaryColor' $secondary
                Set-DrawingProperty $properties 'gradientDirection' $direction
                } else {
                    # Hatch styles are rendered as their source foreground color
                    # until native pattern fills exist in the canvas.
                    Set-DrawingProperty $properties 'fillStyle' 'solid'
                    Set-DrawingProperty $properties 'fillColor' $sourceFill
                }
            }

            $restoredElements.Add($element)
        }

        $elementIndex += if ($drawingEntry.IsPolygon -and $sourceUsesLegacySegments) { $drawingEntry.OriginalSegments } else { $drawingEntry.Segments }
    }

    $definition.elements = @($restoredElements)
    $definition | Add-Member -MemberType NoteProperty -Name groups -Value @($sourceGroups) -Force
    $updated.Add($definition.name)
}

$json = ConvertTo-Json -InputObject $definitions -Depth 100
Set-Content -LiteralPath $DataFile -Value $json -Encoding utf8NoBOM
Write-Output "Updated: $($updated.Count) definitions"
Write-Output "Skipped: $($skipped.Count) definitions"
$skipped | ForEach-Object { Write-Output "- $_" }
