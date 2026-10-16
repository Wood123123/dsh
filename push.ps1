# push.ps1 — 把工作区推到远程仓库
#
# 支持 GitHub / Gitee。会自动：
#   ① 检查远程可达性
#   ② 配置 remote
#   ③ 用 token 认证并推送（token 不写进 git 配置，只在本次调用里用）
#   ④ 失败时给出可操作的提示
#
# 用法：
#   # GitHub（token 从 https://github.com/settings/tokens 生成，勾 repo 权限）
#   .\push.ps1 -Provider github -User <用户名> -Repo dsh -Token <token>
#
#   # Gitee（token 从 https://gitee.com/profile/personal_access_tokens 生成）
#   .\push.ps1 -Provider gitee -User <用户名> -Repo dsh -Token <token>
#
#   # 只推本地裸仓库（不需要任何账号）
#   .\push.ps1 -Provider local
#
#   # 已有远程时只推更新
#   .\push.ps1 -Provider github -User <用户名> -Repo dsh

[CmdletBinding()]
param(
    [ValidateSet('github','gitee','local')]
    [string]$Provider = 'local',
    [string]$User,
    [string]$Repo = 'dsh',
    [string]$Token,
    [string]$BarePath = 'D:\git\dsh.git',
    [switch]$Private = $true
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
if (-not $git) { Bad 'git 未安装或不在 PATH'; exit 1 }

Push-Location $root
try {
    Say ''
    Say ' 推送到远程' 'White'
    Say " 工作区: $root"
    Say " 目标:   $Provider"

    # ── 未提交的改动先提醒 ──
    Head '① 检查工作区状态'
    $dirty = & $git status --porcelain 2>&1
    if (@($dirty).Count -gt 0) {
        Warn ("有 {0} 处未提交的改动" -f @($dirty).Count)
        Say '   要一起提交的话先跑：' 'DarkGray'
        Say '      git add -A; git commit -m "..."' 'DarkGray'
    } else { Ok '工作区干净' }

    # ── local：推本地裸仓库 ──
    if ($Provider -eq 'local') {
        Head '② 推送到本地裸仓库'
        if (-not (Test-Path -LiteralPath $BarePath)) {
            $parent = Split-Path $BarePath -Parent
            if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Force $parent | Out-Null }
            & $git init --bare -q $BarePath
            Ok "已创建裸仓库: $BarePath"
        }
        $remotes = & $git remote 2>&1
        if ($remotes -notcontains 'origin') { & $git remote add origin $BarePath }
        & $git push -q origin --all 2>&1 | ForEach-Object { Write-Output ('   ' + $_) }
        & $git push -q origin --tags 2>&1 | ForEach-Object { Write-Output ('   ' + $_) }
        Ok "已推送到 $BarePath"
        Say ''
        Say '   从这里克隆：' 'DarkGray'
        Say ("     git clone `"{0}`" <目标目录>" -f $BarePath) 'DarkGray'
        return
    }

    # ── 确定远程 URL ──
    $host_ = if ($Provider -eq 'github') { 'github.com' } else { 'gitee.com' }
    if (-not $User) { Bad "请用 -User <用户名> 指定 $host_ 账号"; exit 1 }
    $urlAuth = "https://{0}@{1}/{2}/{3}.git" -f $User, $host_, $User, $Repo
    $urlPlain = "https://{0}/{1}/{2}.git"    -f $host_, $User, $Repo
    $webUrl   = "https://{0}/{1}/{2}"        -f $host_, $User, $Repo

    # ── 可达性 + 仓库是否存在 ──
    Head '② 检查远程'
    Say ("   目标: {0}" -f $webUrl)
    $env:GIT_TERMINAL_PROMPT = '0'
    $ls = & $git ls-remote --heads $urlPlain 2>&1
    if ($LASTEXITCODE -ne 0) {
        Warn '远程仓库不可达或不存在'
        Say ''
        Say '   ── 请先在浏览器里建库（勾选 Private）:' 'Yellow'
        Say ("      {0}/new" -f $webUrl) 'White'
        if ($Provider -eq 'github') { Say ("      {0}/new" -f $webUrl) 'White' }
        Say ''
        Say '   建好后重新运行本脚本。' 'Yellow'
        if ($ls) { Say '' ; Say ("   原始错误: {0}" -f ($ls -join ' | ')) 'DarkGray' }
        exit 1
    }
    Ok '远程可达'

    # ── 配 remote（不带 token）──
    Head '③ 配置 remote'
    $remotes = & $git remote 2>&1
    $rname = if ($Provider -eq 'github') { 'origin' } else { 'gitee' }
    if ($remotes -contains $rname) {
        & $git remote set-url $rname $urlPlain
        Ok "$rname → $urlPlain"
    } else {
        & $git remote add $rname $urlPlain
        Ok "已加 $rname → $urlPlain"
    }

    # ── 推送（token 只在本次命令行里出现）──
    Head '④ 推送'
    if (-not $Token) {
        Warn '未提供 -Token —— 会走交互式认证（可能弹窗或提示输密码）'
        & $git push -u $rname --all
        & $git push $rname --tags
    } else {
        # 用带 token 的临时 URL 推，推完立刻把 remote 改回不带 token 的
        & $git push -q ("https://{0}:{1}@{2}/{0}/{3}.git" -f $User, $Token, $host_, $Repo) --all 2>&1 |
            ForEach-Object { Write-Output ('   ' + ($_ -replace [regex]::Escape($Token), '***')) }
        & $git push -q ("https://{0}:{1}@{2}/{0}/{3}.git" -f $User, $Token, $host_, $Repo) --tags 2>&1 |
            ForEach-Object { Write-Output ('   ' + ($_ -replace [regex]::Escape($Token), '***')) }
        & $git remote set-url $rname $urlPlain
        Ok '已推送（token 未写入配置）'
    }

    # ── 结果 ──
    Head '⑤ 结果'
    $log = & $git log --oneline -3 2>&1
    $log | ForEach-Object { Say ('   ' + $_) }
    Say ''
    Say ("   仓库地址: {0}" -f $webUrl) 'White'
    Say ''
    Say '   在新机器上：' 'DarkGray'
    Say ("     git clone {0} <目标目录>" -f $urlPlain) 'DarkGray'
    Say '     cd <目标目录>' 'DarkGray'
    Say '     powershell -NoProfile -ExecutionPolicy Bypass -File bootstrap.ps1' 'DarkGray'
    Say ''
    Say '   ⚠ 别忘了单独拷用户全局钩子：' 'Yellow'
    Say ("     {0}\.dsh\AGENTS.md" -f $env:USERPROFILE) 'DarkGray'
}
finally { Pop-Location }
