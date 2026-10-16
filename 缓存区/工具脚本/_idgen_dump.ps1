Add-Type -Path "$env:TEMP\cecil\Mono.Cecil.dll"
$rp = New-Object Mono.Cecil.DefaultAssemblyResolver
$rp.AddSearchDirectory("F:\EFT\EscapeFromTarkov_Data\Managed")
$pp = New-Object Mono.Cecil.ReaderParameters
$pp.AssemblyResolver = $rp
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly("F:\EFT\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll", $pp)

function Show-Method($typeName, $methName) {
  foreach ($t in $a.MainModule.GetTypes()) {
    if ($t.Name -ne $typeName) { continue }
    foreach ($m in $t.Methods) {
      if ($m.Name -ne $methName) { continue }
      Write-Output ('=== ' + $t.FullName + '::' + $m.Name + ' -> ' + $m.ReturnType.Name + ' ===')
      if (-not $m.HasBody) { Write-Output '   (no body)'; return }
      foreach ($i in $m.Body.Instructions) {
        $o = ''
        if ($i.Operand -ne $null) { $o = $i.Operand.ToString() }
        if ($o.Length -gt 100) { $o = $o.Substring(0, 100) }
        Write-Output ('   ' + $i.Offset.ToString('X4') + ': ' + $i.OpCode.Name.PadRight(9) + ' ' + $o)
      }
      Write-Output ''
    }
  }
}

Show-Method 'ItemController' 'get_NextId'
Show-Method 'ItemExtensions' 'CloneItemInternal'
Show-Method 'SplitIdGenerator' 'RollBack'
Show-Method 'SplitIdGenerator' '.ctor'
Show-Method 'ItemExtensions' 'CloneItem'

Write-Output '=== ItemController fields ==='
foreach ($t in $a.MainModule.GetTypes()) {
  if ($t.FullName -ne 'EFT.InventoryLogic.ItemController') { continue }
  foreach ($f in $t.Fields) {
    if ($f.Name -match 'Id') { Write-Output ('   ' + $f.FieldType.Name + ' ' + $f.Name) }
  }
}
