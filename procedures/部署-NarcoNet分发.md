# 部署-NarcoNet分发

> **触发条件**：在服务器 `EFT-SERVER\BepInEx\plugins\` 里加了/换了/删了文件，
> 想知道会不会到客户端、要不要手动做什么。

---

## 首选姿势（可直接抄）

### 1. 先确认「会不会分发」

```powershell
# 分发路径（config.yaml: syncPaths）
$ns = '\\Xtzj-20200101vo\c\Users\Wood\Documents\EFT-SERVER\SPT_Runtime\user\mods\NarcoNet-Server'
Select-String -Path "$ns\config.yaml" -Pattern '^\s+- BepInEx/plugins'

# 有没有进清单（两份，普通客户端 + 无头端）
Select-String -Path "$ns\data\NarcoNet_KnownFiles*.txt" -Pattern '<文件名>'
```

**`BepInEx/plugins` 是同步路径 ⇒ 里面每个进了清单的文件都会推到客户端。**

### 2. 换文件后必须同步清单哈希，否则客户端**反复下载**

```powershell
$dll = '<服务器>\BepInEx\plugins\<mod>\<file>'
$newHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLower()

foreach ($kf in @('NarcoNet_KnownFiles.txt','NarcoNet_KnownFiles_headless.txt')) {
    $p = "<NarcoNet-Server>\data\$kf"
    $ls = [System.IO.File]::ReadAllLines($p)
    for ($i = 0; $i -lt $ls.Count; $i++) {
        if ($ls[$i] -match '^(\S.*\|\s*BepInEx/plugins/<mod>/<file>\s*\|\s*)([0-9a-fA-F]{64})(\s*\|)') {
            $ls[$i] = $Matches[1] + $newHash + $Matches[3]
        }
    }
    [System.IO.File]::WriteAllLines($p, $ls, (New-Object System.Text.UTF8Encoding($false)))
}
```

### 3. 删文件后必须**删掉清单条目**（否则 404）

```powershell
# 逐条核对清单里的文件是否还在，不在就整行删掉
$kept = foreach ($line in $ls) {
    if ($line -match '^(\S[^|]*)\|\s*(\S[^|]*?)\s*\|\s*[0-9a-f]{64}\s*\|\s*FILE') {
        $rel = $Matches[2].Trim()
        if (-not (Test-Path (Join-Path $serverRoot ($rel -replace '/','\')))) { continue }
    }
    $line
}
```

**删掉条目 = 客户端会按 `Delete Removed Files = true` 自动删除本地副本** —— 这正是「归档插件」要的效果。

---

## 反面清单（试过不通的）

| 做法 | 结果 |
|---|---|
| 换完 dll 直接开服 | ⚠ 清单哈希还是旧的 ⇒ 客户端下完再比对仍不符 ⇒ **反复下载** |
| 删完文件不动清单 | ⚠ 客户端按清单去下载 ⇒ **404** |
| 只改 `KnownFiles.txt` 不改 `_headless.txt` | ⚠ 无头端还是旧哈希 ⇒ 无头端反复下载 |
| 想找「手动重新生成」按钮 | ❌ config.yaml 只有 `syncPaths` / `profiles` / `exclusions` 三项，**没有生成开关**；清单是**服务器启动时自动重扫**的 |
| 把 `.bak` 放在 `BepInEx\plugins\<mod>\` 下 | ❌ **会被当正式文件分发**，客户端多出一堆垃圾。备份必须放 `<游戏根>\_plugin_backups\` |

---

## 关键机制速查

```
config.yaml 顶层只有三项：
  syncPaths (L72)    —— 哪些目录同步
  profiles  (L225)   —— headless 额外排除哪些（如 Fika.Headless* / DynamicMaps）
  exclusions(L325)   —— 全局排除（spt 核心 / NarcoNet 自己 / SAIN 自动生成文件）

KnownFiles 文件头格式：
  # NarcoNet Server — Known Files Dump
  # Generated: <UTC 时间>
  # Game Root: <服务器根>
  # Profile: (base) | headless
  # Exclusions (53): ...
  # Total: <条目数> entries across <路径数> sync paths

条目格式：
  <同步路径> | <相对文件路径> | <sha256> | FILE

⚠ headless profile 只排除 Fika.Headless*，**不排除 Fika.Core.dll** ⇒ 无头端也收新 Fika
```

## 验证方式（怎么确认真成了）

```powershell
# ① 哈希两边一致
(Get-FileHash '<服务器 dll>' -Algorithm SHA256).Hash.ToLower()
# 对比两份 KnownFiles 里的值

# ② 清单里没有指向不存在文件的条目
# 逐条 Test-Path 一遍

# ③ 反向：分发源里的文件都有清单条目
# （⚠ 会有一批本来就不同步的，如 NarcoNet 自己/spt 核心/被 profile 排除的）
```
