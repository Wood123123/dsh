# bootstrap.ps1 — 在新机器上把本工作区接起来
#
# 用途：换机器后跑一次，自动
#   ① 探测游戏端 / 无头端 / 服务端路径
#   ② 生成 machines.json（机器本地配置，不入库）
#   ③ 校验目录结构与必需文件
#   ④ 打印环境摘要
#
# 用法：
#   cd <工作区>
#   powershell -NoProfile -ExecutionPolicy Bypass -File bootstrap.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File bootstrap.ps1 -GameRoot "D:\EFT" -ServerHost "192.168.1.39"

[CmdletBinding()]
param(
    [string]$GameRoot,            # 客户端游戏根（含 BepInEx）
    [string]$ServerHost,          # 服务端机器名或 IP
    [string]$ServerShare,         # 服务端 SPT_Runtime 的 UNC 路径
    [switch]$Force                # 覆盖已存在的 machines.json
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $root) { $root = (Get-Location).Path }

function Say([string]$m, [string]$c = 'Gray') { Write-Host $m -ForegroundColor $c }
function Ok([string]$m)  { Say "   ✅ $m" 'Green' }
function Warn([string]$m){ Say "   ⚠  $m" 'Yellow' }
function Bad([string]$m) { Say "   ❌ $m" 'Red' }
function Head([string]$m){ Write-Host ''; Say "════ $m ════" 'Cyan' }

Say ''
Say ' dsh 工作区 bootstrap' 'White'
Say " 工作区根: $root"

# ─────────────────────────────────────────────
Head '① 校验目录结构'

$needDirs = @('工作区','缓存区','procedures',
              '缓存区\参考源码','缓存区\备份','缓存区\素材包','缓存区\日志与数据','缓存区\工具脚本')
$missDir = @()
foreach ($d in $needDirs) {
    if (Test-Path -LiteralPath (Join-Path $root $d)) { Ok $d } else { Bad "$d （缺失）"; $missDir += $d }
}

$needFiles = @('AGENTS.md','_索引.md','procedures\INDEX.md','procedures\_模板.md','procedures\_inbox.md')
$missFile = @()
foreach ($f in $needFiles) {
    if (Test-Path -LiteralPath (Join-Path $root $f)) { Ok $f } else { Bad "$f （缺失）"; $missFile += $f }
}

if ($missDir.Count -gt 0) {
    Warn '缺失的目录会自动创建'
    foreach ($d in $missDir) { New-Item -ItemType Directory -Force (Join-Path $root $d) | Out-Null }
}

# ─────────────────────────────────────────────
Head '② 探测游戏端路径'

function Test-GameRoot([string]$p) {
    if (-not $p) { return $false }
    return (Test-Path -LiteralPath (Join-Path $p 'EscapeFromTarkov.exe')) -or
           (Test-Path -LiteralPath (Join-Path $p 'BepInEx'))
}

if (-not $GameRoot) {
    $cands = @()
    # 从 machines.json 里读旧值
    $mj = Join-Path $root 'machines.json'
    if (Test-Path -LiteralPath $mj) {
        try {
            $old = [System.IO.File]::ReadAllText($mj, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            if ($old.client.gameRoot) { $cands += $old.client.gameRoot }
        } catch { }
    }
    $cands += @('F:\EFT','D:\EFT','C:\EFT','E:\EFT','C:\Games\EFT','D:\Games\EFT',
                "$env:USERPROFILE\Desktop\EFT")
    foreach ($c in $cands) { if (Test-GameRoot $c) { $GameRoot = $c; break } }
}

if (Test-GameRoot $GameRoot) {
    Ok "客户端游戏根: $GameRoot"
} else {
    Warn '没自动找到游戏端 —— 请用 -GameRoot "<路径>" 重新运行'
    $GameRoot = $GameRoot  # 保留原值（可能为空）
}

# ─────────────────────────────────────────────
Head '③ 探测服务端 / 无头端'

if (-not $ServerHost) {
    $mj = Join-Path $root 'machines.json'
    if (Test-Path -LiteralPath $mj) {
        try {
            $old = [System.IO.File]::ReadAllText($mj, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
            if ($old.server.host) { $ServerHost = $old.server.host }
        } catch { }
    }
}

function Test-Unc([string]$p) {
    if (-not $p) { return $false }
    try { return Test-Path -LiteralPath $p } catch { return $false }
}

$headless = @()
if ($ServerHost) {
    $srvBase = "\\$ServerHost\c\Users\Wood\Documents"
    if (-not $ServerShare) {
        foreach ($c in @("$srvBase\EFT-SERVER\SPT_Runtime", "$srvBase\EFT-SERVER\SPT_Runtime")) {
            if (Test-Unc $c) { $ServerShare = $c; break }
        }
    }
    if (Test-Unc $ServerShare) { Ok "服务端 SPT_Runtime: $ServerShare" } else { Warn "服务端不可达: $ServerShare" }

    foreach ($h in @('EFT-headless','EFT-headless1')) {
        $hr = "$srvBase\$h"
        if (Test-Unc $hr) {
            Ok "无头端可达: $hr"
            $headless += [pscustomobject]@{ label = $h; root = $hr }
        } else { Warn "无头端不可达: $hr" }
    }
} else {
    Warn '没指定服务端主机 —— 用 -ServerHost "<机器名或IP>" 重新运行'
}

# ─────────────────────────────────────────────
Head '④ 生成 machines.json（机器本地，不入库）'

$mjPath = Join-Path $root 'machines.json'
if ((Test-Path -LiteralPath $mjPath) -and -not $Force) {
    Warn 'machines.json 已存在 —— 保留原文件（要覆盖加 -Force）'
} else {
    $cfg = [ordered]@{
        '_说明' = '机器本地路径。换机器后跑 bootstrap.ps1 重新探测生成。'
        client = [ordered]@{
            label         = 'Wood 客户端'
            gameRoot      = $GameRoot
            plugins       = if ($GameRoot) { Join-Path $GameRoot 'BepInEx\plugins' } else { '' }
            config        = if ($GameRoot) { Join-Path $GameRoot 'BepInEx\config'  } else { '' }
            log           = if ($GameRoot) { Join-Path $GameRoot 'BepInEx\LogOutput.log' } else { '' }
            pluginBackups = if ($GameRoot) { Join-Path $GameRoot '_plugin_backups' } else { '' }
        }
        server = [ordered]@{
            label    = 'SPT 服务端'
            host     = $ServerHost
            runtime  = $ServerShare
            mods     = if ($ServerShare) { Join-Path $ServerShare 'user\mods' } else { '' }
            database = if ($ServerShare) { Join-Path $ServerShare 'SPT_Data\database' } else { '' }
            configs  = if ($ServerShare) { Join-Path $ServerShare 'SPT_Data\configs' } else { '' }
            httpPort = 6969
        }
        headless = @($headless | ForEach-Object {
            [ordered]@{
                label         = $_.label
                root          = $_.root
                plugins       = Join-Path $_.root 'BepInEx\plugins'
                log           = Join-Path $_.root 'BepInEx\LogOutput.log'
                pluginBackups = Join-Path $_.root '_plugin_backups'
            }
        })
        env = [ordered]@{
            sptVersion   = '4.1.6'
            eftVersion   = '0.16.9.40743'
            fikaClient   = '2.4.5'
            fikaServer   = '2.4.2'
            serverLogTz  = 'UTC（本地 = +8）'
            shell        = 'Windows PowerShell 5.1'
        }
    }
    $json = $cfg | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($mjPath, $json, (New-Object System.Text.UTF8Encoding($false)))
    Ok "已生成 machines.json"
}

# ─────────────────────────────────────────────
Head '⑤ 确保 machines.json 不被提交'

$gi = Join-Path $root '.gitignore'
if (Test-Path -LiteralPath $gi) {
    $t = [System.IO.File]::ReadAllText($gi, [System.Text.Encoding]::UTF8)
    if ($t -notmatch 'machines\.json') {
        Add-Content -LiteralPath $gi -Value "`n# 机器本地路径，不入库`nmachines.json" -Encoding UTF8
        Ok '已把 machines.json 加入 .gitignore'
    } else { Ok 'machines.json 已在 .gitignore' }
} else { Warn '.gitignore 不存在' }

# ─────────────────────────────────────────────
Head '⑥ 环境摘要'

$g = Get-Command git -ErrorAction SilentlyContinue
Say ("   git:        {0}" -f $(if ($g) { $g.Source } else { '未安装' }))
Say ("   shell:      PowerShell {0}" -f $PSVersionTable.PSVersion)
Say ("   工作区根:   {0}" -f $root)
Say ("   游戏端:     {0}" -f $(if (Test-GameRoot $GameRoot) { $GameRoot } else { '⚠ 未确定' }))
Say ("   服务端:     {0}" -f $(if (Test-Unc $ServerShare) { $ServerShare } else { '⚠ 不可达' }))
# 只数「我们的」说明文件，排除第三方/素材包里的 README
$docs = @(Get-ChildItem $root -Recurse -File -Filter 'README.md' -ErrorAction SilentlyContinue |
          Where-Object {
              $_.FullName -notlike '*\缓存区\素材包\*' -and
              $_.FullName -notlike '*\缓存区\参考源码\_fika_src\*' -and
              $_.FullName -notlike '*\缓存区\参考源码\_sain\*' -and
              $_.FullName -notlike '*\缓存区\参考源码\_manimal\*' -and
              $_.FullName -notlike '*\.git\*'
          })
Say ("   说明文件:   {0} 份 README.md" -f $docs.Count)
$cards = @(Get-ChildItem (Join-Path $root 'procedures') -File -Filter '*.md' -ErrorAction SilentlyContinue |
           Where-Object { $_.Name -notmatch '^(_|INDEX)' })
Say ("   经验卡:     {0} 张" -f $cards.Count)

Write-Host ''
Say ' ────────────────────────────────────────' 'DarkGray'
Say ' 接下来：让你的 AI 助手先读 _索引.md' 'White'
Say ' ────────────────────────────────────────' 'DarkGray'
Write-Host ''
