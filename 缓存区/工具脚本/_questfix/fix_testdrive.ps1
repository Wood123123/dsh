# 按【条件】补齐试驾系列中文文本（不改条件本身）
$ErrorActionPreference = 'Stop'
$OutputEncoding = [System.Text.Encoding]::UTF8

$srv    = '\\Xtzj-20200101vo\c\Users\Wood\Documents\EFT-SERVER\SPT_Runtime'
$qbCh   = Join-Path $srv 'user\mods\QuestBackport\db\locales\ch.json'
$baseCh = Join-Path $srv 'SPT_Data\database\locales\global\ch.json'
$stamp  = Get-Date -Format 'yyyyMMdd_HHmmss'

# 每个任务：目标文本 key / 旧文本 / 新文本 / 修改原因（全部依据条件）
$fixes = @(
    [pscustomobject]@{
        N = '试驾-1'; K = '5c1b765d86f77413193fa4f2'
        O = '在灯塔或储备站，使用装有 Hybrid 46 消音器和 PM II 1-8x24 瞄准镜的 SR-25 步枪消灭 PMC 行动人员'
        Nw = '在灯塔或储备站，使用装有 Hybrid 46 消音器和 PM II 1-8x24 瞄准镜的 SR-25 步枪消灭 6 名 PMC 行动人员'
        W = '补次数 6（CounterCreator value=6）'
    }
    [pscustomobject]@{
        N = '试驾-2'; K = '63a5cf262964a7488f5243d1'
        O = '在森林、中心区、立交桥或海关，使用装有 SD 上机匣和 Aimpoint PRO 瞄具的 MP5 消灭 PMC 行动人员'
        Nw = '在森林、中心区、立交桥或海关，使用装有 SD 上机匣和 Aimpoint PRO 瞄具的 MP5（海军三发点射）消灭 20 名 PMC 行动人员'
        W = '补次数 20 + 武器精确化（条件 weapon 只接受「海军三发点射」变体）'
    }
    [pscustomobject]@{
        N = '试驾-3'; K = '64f5deac39e45b527a7c4235'
        O = '在塔科夫街区、海岸线或中心区，使用装有消音器和 Valday PS-320 1/6x 瞄准镜的 AK-12 消灭 PMC 行动人员'
        Nw = '在塔科夫街区、海岸线或中心区，使用装有 AK-12 消音器和 Valday PS-320 1/6x 瞄准镜的 AK-12 消灭 10 名 PMC 行动人员'
        W = '补次数 10 + 消音器精确化为「AK-12 消音器」'
    }
    [pscustomobject]@{
        N = '试驾-4'; K = '6574e0dedc0d635f633a5807'
        O = '在海岸线、灯塔或储备站，使用装有 Axion Kobra EKP-1S-03 反射式瞄具的 RPDN 机枪消灭任意目标'
        Nw = '在海岸线、灯塔或储备站，使用装有 Axion Kobra EKP-1S-03 反射式瞄具的 RPDN 机枪消灭 40 个任意目标'
        W = '补次数 40（目标类型 Any）'
    }
    [pscustomobject]@{
        N = '试驾-5'; K = '66a0f5a7f9eae6761253114c'
        O = '在储备站或灯塔，使用装有 SV-1381 消音器和 KP-SR2 反射式瞄具的 SR-2M 消灭任意目标'
        Nw = '在储备站或灯塔，使用装有 SV-1381 消音器和 KP-SR2 反射式瞄具的 SR-2M 消灭 30 个任意目标'
        W = '补次数 30'
    }
    [pscustomobject]@{
        N = '试驾-6'; K = '675c1ff1a757ddd00404f0ae'
        O = '在灯塔或实验室，使用 Aklys Defense「迅猛龙」消灭任意目标'
        Nw = '在灯塔或实验室，使用 Aklys Defense「迅猛龙」消灭 30 个任意目标'
        W = '补次数 30（条件无配件要求，故文本不提配件）'
    }
)

foreach ($file in @($qbCh, $baseCh)) {
    $bak = "$file.bak3_$stamp"
    Copy-Item -LiteralPath $file -Destination $bak -Force
    Write-Output ('════════ {0}' -f $file.Replace($srv, '…'))
    Write-Output ('   备份: {0}' -f (Split-Path $bak -Leaf))

    $t = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
    $total = 0
    foreach ($fx in $fixes) {
        $oldPair = '"' + $fx.K + '":"' + $fx.O + '"'
        $newPair = '"' + $fx.K + '":"' + $fx.Nw + '"'
        $cnt = ([regex]::Matches($t, [regex]::Escape($oldPair))).Count
        if ($cnt -gt 0) {
            $t = $t.Replace($oldPair, $newPair)
            $total += $cnt
            Write-Output ('   ✅ {0}   原因: {1}' -f $fx.N, $fx.W)
        }
        else {
            Write-Output ('   -- {0} 未命中' -f $fx.N)
        }
    }
    if ($total -gt 0) {
        [System.IO.File]::WriteAllText($file, $t, (New-Object System.Text.UTF8Encoding($false)))
        Write-Output ('   → 已写入，共改 {0} 处' -f $total)
    }
    Write-Output ''
}

Write-Output '════════ 最终校验（QuestBackport 侧）════════'
$t2 = [System.IO.File]::ReadAllText($qbCh, [System.Text.Encoding]::UTF8)
foreach ($fx in $fixes) {
    $m = [regex]::Match($t2, '"' + [regex]::Escape($fx.K) + '"\s*:\s*"((?:[^"\\]|\\.)*)"')
    if ($m.Success) { Write-Output ('  {0} : {1}' -f $fx.N, $m.Groups[1].Value) }
    else { Write-Output ('  {0} : (无)' -f $fx.N) }
}
