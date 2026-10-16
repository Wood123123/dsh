param([string]$Path, [string]$Pattern, [int]$Tail = 0, [int]$Context = 0, [int]$Max = 200)
$fs = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
$sr = New-Object System.IO.StreamReader($fs, [System.Text.Encoding]::UTF8)
$lines = New-Object System.Collections.Generic.List[string]
while ($null -ne ($l = $sr.ReadLine())) { $lines.Add($l) }
$sr.Close(); $fs.Close()
Write-Output ("FILE=" + $Path + "  LINES=" + $lines.Count)
$start = 0
if ($Tail -gt 0) { $start = [Math]::Max(0, $lines.Count - $Tail) }
$hits = 0
for ($i = $start; $i -lt $lines.Count; $i++) {
  $l = $lines[$i]
  if (($Pattern -eq '') -or ($l -match $Pattern)) {
    $a = [Math]::Max($start, $i - $Context)
    $b = [Math]::Min($lines.Count - 1, $i + $Context)
    for ($j = $a; $j -le $b; $j++) {
      $s = $lines[$j]
      if ($s.Length -gt 300) { $s = $s.Substring(0, 300) + ' ...[TRUNC]' }
      Write-Output (($j + 1).ToString() + ": " + $s)
    }
    if ($Context -gt 0) { Write-Output "---" }
    $hits++
    if ($hits -ge $Max) { break }
  }
}
