$csv = "F:\EFT\BepInEx\plugins\maschine-ModProfiler\exports\20261010-101317\modprofiler-20261010-103536.csv"
$meta = "F:\EFT\BepInEx\plugins\maschine-ModProfiler\exports\20261010-101317\modprofiler-20261010-103536.meta.json"

# UI / stash / menu / result-screen / input patches -- user says these frames do not matter
$uiKeys = @(
  'GridView.', 'ItemView.Update', 'CompactCharacteristicPanel', 'StashBehaviour', 'MenuTaskBar',
  'PlayerModelView.Show', 'RaidPlayer.UpdateVisualRepresentation', 'MainMenuUIScript',
  'SessionResultExitStatus', 'OverallScreen.Show', 'ItemUiContext', 'InventoryScreen',
  'SlotView.SetSlotGraphics', 'RelatedRequirements', 'GridItemView.', 'TaxonomyColorExtension',
  'ConfigurationManager.OnGUI', 'SimpleTooltip', 'NotifierView', 'ItemDeserializer',
  'ItemFactory.CreateItem', 'InventoryEquipment.GetTotalWeight', 'InputKey.Update',
  'MenuUI', 'LoadingScreen', 'Matchmaker', 'EftClientBackendSession', 'TarkovApplication',
  'PrepareItems', 'InventoryClose', 'InventoryOpen', 'SortingTable', 'ItemSorter'
)
# Hotkey map -- separate bucket
$mapKeys = @('MapPeekComponent', 'MapScreen', 'DynamicMaps')

function Classify($method, $target) {
  $s = "$method $target"
  foreach ($k in $mapKeys) { if ($s -like "*$k*") { return 'MAP' } }
  foreach ($k in $uiKeys)  { if ($s -like "*$k*") { return 'UI' } }
  return 'GAMEPLAY'
}

$rows = Import-Csv $csv
$rows | ForEach-Object { $_ | Add-Member -NotePropertyName bucket -NotePropertyValue (Classify $_.method $_.target) -Force }

Write-Output '########## A. patch buckets (sum of ms_per_frame) ##########'
foreach ($b in @('GAMEPLAY','MAP','UI')) {
  $g = $rows | Where-Object { $_.bucket -eq $b }
  $pf = ($g | Measure-Object ms_per_frame -Sum).Sum
  $calls = ($g | Measure-Object calls -Sum).Sum
  Write-Output ("  {0,-9} patches {1,5} | {2,8:N4} ms/frame | calls {3,12:N0}" -f $b, $g.Count, $pf, $calls)
}

Write-Output ''
Write-Output '########## B. GAMEPLAY ONLY: per-mod ms/frame ##########'
$gp = $rows | Where-Object { $_.bucket -eq 'GAMEPLAY' -and $_.mod -ne 'EFT (vanilla)' }
$gp | Group-Object mod | ForEach-Object {
  [pscustomobject]@{
    Mod        = $_.Name
    Patches    = $_.Count
    MsPerFrame = [math]::Round((($_.Group | Measure-Object ms_per_frame -Sum).Sum),4)
    TotalMs    = [math]::Round((($_.Group | Measure-Object total_ms -Sum).Sum),1)
    Calls      = ($_.Group | Measure-Object calls -Sum).Sum
    WorstFrame = [math]::Round((($_.Group | Measure-Object max_ms_in_frame -Maximum).Maximum),2)
  }
} | Sort-Object MsPerFrame -Descending | Select-Object -First 20 | Format-Table -AutoSize

Write-Output ''
Write-Output '########## C. GAMEPLAY ONLY: top 25 patches ##########'
$gp | Sort-Object { [double]$_.ms_per_frame } -Descending | Select-Object -First 25 | ForEach-Object {
  Write-Output ("  {0,7} ms/f | {1,10} ms tot | {2,6}us/call | {3,9} calls | max/frame {4,8} | {5} [{6}] :: {7} -> {8}" -f `
    $_.ms_per_frame, $_.total_ms, $_.avg_us_per_call, $_.calls, $_.max_ms_in_frame, $_.mod, $_.kind, $_.method, $_.target)
}

Write-Output ''
Write-Output '########## D. SPIKE buckets (in-raid only, frame < 68750) ##########'
$raw = [System.IO.File]::ReadAllText($meta).TrimStart([char]0xFEFF,[char]0xEF,[char]0xBB,[char]0xBF)
$raw = $raw.Substring($raw.IndexOf('{'))
$j = $raw | ConvertFrom-Json
$inRaid = $j.spikes | Where-Object { [int]$_.frame -lt 68750 }
$classed = $inRaid | ForEach-Object {
  $t = ($_.top | Select-Object -First 1)
  [pscustomobject]@{
    Frame   = [int]$_.frame
    Sec     = [double]$_.seconds
    FrameMs = [double]$_.frame_ms
    Gc      = $_.gc
    Mod     = $(if ($t) { $t.mod } else { '(none)' })
    Method  = $(if ($t) { $t.method } else { '' })
    Ms      = $(if ($t) { [double]$t.ms } else { 0 })
    Bucket  = $(if ($t) { Classify $t.method $t.method } else { 'NONE' })
  }
}
foreach ($b in @('GAMEPLAY','MAP','UI','NONE')) {
  $g = $classed | Where-Object { $_.Bucket -eq $b }
  Write-Output ("  {0,-9} spikes {1,4} | attributed {2,8:N1} ms" -f $b, $g.Count, (($g | Measure-Object Ms -Sum).Sum))
}

Write-Output ''
Write-Output '  === in-raid GAMEPLAY spikes (this is what matters) ==='
$classed | Where-Object { $_.Bucket -eq 'GAMEPLAY' } | Sort-Object FrameMs -Descending | ForEach-Object {
  Write-Output ("    t={0,7}s frame {1,-6} stutter {2,7}ms gc={3,-5} <- {4} / {5} = {6}ms" -f $_.Sec, $_.Frame, $_.FrameMs, $_.Gc, $_.Mod, $_.Method, [math]::Round($_.Ms,2))
}

Write-Output ''
Write-Output '  === in-raid GAMEPLAY spikes grouped by mod ==='
$classed | Where-Object { $_.Bucket -eq 'GAMEPLAY' } | Group-Object Mod | ForEach-Object {
  [pscustomobject]@{
    Mod = $_.Name
    Count = $_.Count
    SumMs = [math]::Round((($_.Group | Measure-Object Ms -Sum).Sum),1)
    WorstMs = [math]::Round((($_.Group | Measure-Object Ms -Maximum).Maximum),1)
  }
} | Sort-Object SumMs -Descending | Format-Table -AutoSize

Write-Output ''
Write-Output '  === in-raid NON-attributed spikes (no patch above ~1ms) ==='
$classed | Where-Object { $_.Bucket -eq 'NONE' -or $_.Ms -le 1.0 } | Sort-Object FrameMs -Descending | Select-Object -First 20 | ForEach-Object {
  Write-Output ("    t={0,7}s frame {1,-6} stutter {2,7}ms gc={3,-5} top={4} {5}ms" -f $_.Sec, $_.Frame, $_.FrameMs, $_.Gc, $_.Mod, [math]::Round($_.Ms,2))
}
