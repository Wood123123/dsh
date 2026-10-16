Add-Type -Path "$env:TEMP\cecil\Mono.Cecil.dll" -ErrorAction Stop

$Dll = $env:DUMP_DLL
$Targets = $env:DUMP_TARGETS -split ';'

$asm = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($Dll)
Write-Output ("ASSEMBLY: " + $asm.Name.Name + " v" + $asm.Name.Version)
Write-Output ("PATH: " + $Dll)
Write-Output ""

foreach ($t in $Targets) {
  if ([string]::IsNullOrWhiteSpace($t)) { continue }
  $parts = $t -split '::'
  $typeName = $parts[0]
  $methodName = if ($parts.Count -gt 1) { $parts[1] } else { $null }

  $types = @($asm.MainModule.GetTypes() | Where-Object { $_.Name -eq $typeName })
  if ($types.Count -eq 0) { Write-Output "!! type not found: $typeName"; continue }

  foreach ($ty in $types) {
    if (-not $methodName) {
      Write-Output ("=== TYPE " + $ty.FullName + " : base=" + $ty.BaseType.Name + " ===")
      foreach ($f in $ty.Fields) { Write-Output ("   field  " + $f.FieldType.Name + " " + $f.Name) }
      foreach ($p in $ty.Properties) { Write-Output ("   prop   " + $p.PropertyType.Name + " " + $p.Name) }
      continue
    }

    $ms = @($ty.Methods | Where-Object { $_.Name -eq $methodName })
    if ($ms.Count -eq 0) { Write-Output "!! method not found: $typeName::$methodName"; continue }

    foreach ($m in $ms) {
      $ps = @()
      foreach ($p in $m.Parameters) {
        $s = $p.ParameterType.ToString() + " " + $p.Name
        if ($p.ParameterType.IsByRef) { $s += " [BYREF]" }
        $ps += $s
      }
      $gp = ""
      if ($m.HasGenericParameters) { $gp = "<" + (($m.GenericParameters | ForEach-Object { $_.Name }) -join ",") + ">" }
      Write-Output ("=== " + $ty.Name + "::" + $m.Name + $gp + "(" + ($ps -join ", ") + ") -> " + $m.ReturnType.Name + " ===")

      if (-not $m.HasBody) { Write-Output "   (no body)"; Write-Output ""; continue }

      foreach ($i in $m.Body.Instructions) {
        if ($i.OpCode.Name -notmatch '^(call|callvirt|newobj|ldstr|ldfld|stfld|ret|throw)$') { continue }
        $o = ""
        if ($i.Operand -ne $null) {
          $o = $i.Operand.ToString()
          if ($o.Length -gt 115) { $o = $o.Substring(0, 115) + "..." }
        }
        Write-Output ("   " + $i.Offset.ToString("X4") + ": " + $i.OpCode.Name.PadRight(9) + " " + $o)
      }
      Write-Output ""
    }
  }
}
