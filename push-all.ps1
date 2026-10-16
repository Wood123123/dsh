# push-all.ps1 — 一键推送全部仓库到远程
#
# 本工作区有 4 个 git 仓库：
#   ① dsh                        （元仓库：索引、文档、经验卡、未发布的 mod）
#   ② HeadlessPanel              （发布用）
#   ③ FikaInventoryResync        （发布用）
#   ④ TacticalSprint-Companions  （发布用）
#
# 用法：
#   # 只推本地裸仓库（零依赖，随时可做）
#   .\push-all.ps1
#
#   # 推到 Gitee（需要先建好 4 个空仓库 + 生成 token）
#   .\push-all.ps1 -Provider gitee -User <用户名> -Token <token>
#
#   # 推到 GitHub
#   .\push-all.ps1 -Provider github -User <用户名> -Token <token>
#
#   # 只推某几个
#   .\push-all.ps1 -Only dsh,HeadlessPanel

[CmdletBinding()]
param(
    [ValidateSet('local','gitee','github')]
    [string]$Provider = 'local',
    [string]$User,
    [string]$Token,
    [string[]]$Only
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $root) { $root = (Get-Location).Path }

function Say([string]$m, [string]$c = 'Gray') { Write-Host $m -ForegroundColor $c }
function Ok([string]$m)  { Say "   ✅ $m" 'Green' }
function Warn([string]$m){ Say "   ⚠  $m" 'Yellow' }
function Bad([string]$m) { Say "   ❌ $m" 'Red' }
function Head([string]$m){ Write-Host ''; Say "════ $m ════" 'Cyan' }

$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) { Bad 'git 未安装'; exit 1 }

# ── 仓库清单：名字 / 路径 / 远程仓库名 / 一句话描述 ──
$repos = @(
    @{ Name='dsh';                        Path=$root;                                        Desc='SPT/Fika 工作区元仓库：索引、文档、经验卡' }
    @{ Name='HeadlessPanel';              Path=(Join-Path $root '工作区\headless-panel');      Desc='SPT/Fika 服务端 Web 面板' }
    @{ Name='FikaInventoryResync';        Path=(Join-Path $root '工作区\fika-inventory-resync'); Desc='修复 Fika 联机物品栏失同步' }
    @{ Name='TacticalSprint-Companions';  Path=(Join-Path $root '工作区\TacticalSprint-Companions'); Desc='Tactical Sprint 的同步/负重/速度修正' }
)

if ($Only) { $repos = @($repos | Where-Object { $Only -contains $_.Name }) }

Say ''
Say ' 推送全部仓库' 'White'
Say (" 模式: {0}" -f $Provider)
Say (" 仓库数: {0}" -f $repos.Count)

$fail = @()

foreach ($r in $repos) {
    Head ("{0}" -f $r.Name)

    if (-not (Test-Path -LiteralPath (Join-Path $r.Path '.git'))) {
        Warn "不是 git 仓库，跳过：$($r.Path)"
        continue
    }

    Push-Location $r.Path
    try {
        # ── 未提交改动提醒 ──
        $dirty = @(& $git status --porcelain 2>&1)
        if ($dirty.Count -gt 0) { Warn ("{0} 处未提交改动" -f $dirty.Count) }

        # ── 本地裸仓库 ──
        if ($Provider -eq 'local') {
            $bare = "D:\git\$($r.Name).git"
            if (-not (Test-Path -LiteralPath $bare)) {
                & $git init --bare -q $bare
                Ok "新建裸仓库 $bare"
            }
            $remotes = & $git remote 2>&1
            if ($remotes -contains 'origin') { & $git remote set-url origin $bare }
            else { & $git remote add origin $bare }
            & $git push -q origin --all 2>&1 | ForEach-Object { Write-Output ('   ' + $_) }
            & $git push -q origin --tags 2>&1 | Out-Null
            Ok "→ $bare"
            continue
        }

        # ── 云端 ──
        $host_ = if ($Provider -eq 'github') { 'github.com' } else { 'gitee.com' }
        if (-not $User) { Bad '需要 -User <用户名>'; $fail += $r.Name; continue }

        $urlPlain = "https://{0}/{1}/{2}.git" -f $host_, $User, $r.Name

        # 检查远程是否存在
        $env:GIT_TERMINAL_PROMPT = '0'
        $ls = & $git ls-remote --heads $urlPlain 2>&1
        if ($LASTEXITCODE -ne 0) {
            Warn "远程不存在：$urlPlain"
            Say ("      请先建库：https://{0}/projects/new  （名字 {1}，选私有）" -f $host_, $r.Name) 'DarkGray'
            $fail += $r.Name
            continue
        }

        $remotes = & $git remote 2>&1
        if ($remotes -contains 'origin') { & $git remote set-url origin $urlPlain }
        else { & $git remote add origin $urlPlain }

        # 推送（token 不落盘）
        $pushed = $false
        if ($Token) {
            $urlAuth = "https://{0}:{1}@{2}/{0}/{3}.git" -f $User, $Token, $host_, $r.Name
            $o1 = & $git push -q $urlAuth --all 2>&1
            $o2 = & $git push -q $urlAuth --tags 2>&1
            $o1 + $o2 | ForEach-Object { Write-Output ('   ' + ($_ -replace [regex]::Escape($Token), '***')) }
            $pushed = ($LASTEXITCODE -eq 0)
        } else {
            $o = & $git push -q origin --all 2>&1
            $o | ForEach-Object { Write-Output ('   ' + $_) }
            & $git push -q origin --tags 2>&1 | Out-Null
            $pushed = ($LASTEXITCODE -eq 0)
        }

        if ($pushed) {
            & $git remote set-url origin $urlPlain   # 确保不带 token
            Ok "已推送  HEAD=$(& $git rev-parse --short HEAD 2>&1)"
        } else {
            Bad '推送失败'
            $fail += $r.Name
        }
    }
    finally { Pop-Location }
}

# ── 汇总 ──
Head '汇总'
foreach ($r in $repos) {
    Push-Location $r.Path
    try {
        $n = @(& $git ls-files 2>&1).Count
        $head_ = & $git rev-parse --short HEAD 2>&1
        $mark = if ($fail -contains $r.Name) { '❌' } else { '✅' }
        Say ("   {0} {1,-28} {2,3} 文件  HEAD={3}" -f $mark, $r.Name, $n, $head_)
    }
    finally { Pop-Location }
}

if ($fail.Count -gt 0) {
    Write-Host ''
    Warn ("失败: {0}" -f ($fail -join ', '))
    Say '   云端推送前需要先手动建好空仓库（不要勾选初始化 README）' 'DarkGray'
}

Write-Host ''
Say ' 完成后记得：换机器时单独拷 ~/.dsh/AGENTS.md' 'Yellow'
Write-Host ''
