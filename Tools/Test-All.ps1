# Checks run by Deploy-Mod before deploying: translation keys match the source, no tiny fonts, keyed XML whitespace, preview size.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$failures = New-Object System.Collections.Generic.List[string]

$sourceText = (Get-ChildItem (Join-Path $root 'Source') -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"

# Keys used in code: literal keys, settings rows built from a stem, and the enum-driven dropdown options
$prefix = 'CM_Semi_Random_Research_'
$used = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in [regex]::Matches($sourceText, '"(CM_Semi_Random_Research_[A-Za-z0-9_]+[A-Za-z0-9])"')) { [void]$used.Add($m.Groups[1].Value) }
foreach ($m in [regex]::Matches($sourceText, '(?:Checkbox|IntSetting)\(listing, "([A-Za-z_]+)"')) {
    [void]$used.Add("${prefix}Setting_$($m.Groups[1].Value)_Label")
    [void]$used.Add("${prefix}Setting_$($m.Groups[1].Value)_Description")
}
$settingsText = [IO.File]::ReadAllText((Join-Path $root 'Source\SemiRandomResearchSettings.cs'))
foreach ($pair in @(@('ManualReroll', 'Manual_Reroll_'), @('ProgressAddsChoice', 'Progress_Adds_Choice_'))) {
    $body = [regex]::Match($settingsText, "enum $($pair[0])\s*\{([^}]*)\}").Groups[1].Value
    foreach ($name in ($body -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }) {
        [void]$used.Add("${prefix}Setting_$($pair[1])$($name)_Label")
        [void]$used.Add("${prefix}Setting_$($pair[1])$($name)_Description")
    }
}

$keyed = New-Object System.Collections.Generic.HashSet[string]
foreach ($file in Get-ChildItem (Join-Path $root 'Languages\English\Keyed') -Filter *.xml) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '[ \t]+\r?\n') { $failures.Add("Trailing whitespace in $($file.Name)") }
    if ($text -match '\r?\n[ \t]*</') {
        foreach ($m in [regex]::Matches($text, '<(CM_[A-Za-z0-9_]+)>[^<]*\n[ \t]*</')) { $failures.Add("Value ends in a line break: $($m.Groups[1].Value)") }
    }
    [xml]$xml = $text
    foreach ($node in $xml.LanguageData.ChildNodes) {
        if ($node.NodeType -eq 'Element' -and -not $keyed.Add($node.Name)) { $failures.Add("Duplicate key $($node.Name)") }
    }
}

foreach ($key in $used) { if (-not $keyed.Contains($key)) { $failures.Add("Missing translation: $key") } }
foreach ($key in $keyed) { if (-not $used.Contains($key)) { $failures.Add("Unused translation: $key") } }

if ($sourceText -match 'GameFont\.Tiny') { $failures.Add('GameFont.Tiny is used') }

$preview = Join-Path $root 'About\Preview.png'
if ((Get-Item $preview).Length -ge 1000000) { $failures.Add('About\Preview.png is 1,000,000 bytes or more') }

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "FAIL $_" }
    exit 1
}
Write-Host "Test-All passed: $($keyed.Count) keys checked"
