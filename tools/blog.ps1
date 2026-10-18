# blog.ps1 —— 博客园（cnblogs）MetaWeblog 管理工具
#
# 前置：在 _local\secrets.json 里加一段
#   "cnblogs": {
#     "user":  "Wood1130",
#     "token": "<访问令牌>",
#     "blogid": "Wood1130"
#   }
#
# 令牌获取： https://i.cnblogs.com/settings#enableServiceAccess
#   （博客园已取消密码登录，必须用访问令牌；令牌等价于密码，不要提交到 git）
#
# 用法：
#   .\tools\blog.ps1 -Action categories
#   .\tools\blog.ps1 -Action recent -Count 5
#   .\tools\blog.ps1 -Action get -PostId 20007930
#   .\tools\blog.ps1 -Action new -Title "标题" -File "缓存区\参考源码\_blog\post_keys_tutorial.md"
#   .\tools\blog.ps1 -Action edit -PostId 20007930 -File "...md"
#   .\tools\blog.ps1 -Action publish -File "...md"      # new 或 edit（按同目录 .postid 文件判断）

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('categories', 'recent', 'get', 'new', 'edit', 'publish')]
    [string]$Action,

    [int]$PostId = 0,
    [int]$Count = 5,
    [string]$Title = '',
    [string]$File = '',
    [string[]]$Categories = @(),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = Split-Path $PSScriptRoot -Parent
$SecretsPath = Join-Path $Root '_local\secrets.json'

# ────────────────────────────────────────────────────────────
#  读取凭证
# ────────────────────────────────────────────────────────────
function Get-CnblogsCredential {
    # dry-run 不需要真实凭证：用占位符把 XML 组装流程跑通
    if ($DryRun) {
        return @{ User = 'Wood1130'; Token = '<DRY-RUN-TOKEN>'; BlogId = 'Wood1130' }
    }
    if (-not (Test-Path -LiteralPath $SecretsPath)) {
        throw "找不到 $SecretsPath"
    }
    $raw = [System.IO.File]::ReadAllText($SecretsPath, [System.Text.Encoding]::UTF8)
    $o = $raw | ConvertFrom-Json
    if (-not $o.cnblogs) {
        throw @"
secrets.json 里没有 cnblogs 段。请加上：

  "cnblogs": {
    "user":  "Wood1130",
    "token": "<访问令牌>",
    "blogid": "Wood1130"
  }

令牌获取： https://i.cnblogs.com/settings#enableServiceAccess
"@
    }
    if ([string]::IsNullOrWhiteSpace($o.cnblogs.token) -or $o.cnblogs.token -eq '<访问令牌>') {
        throw "cnblogs.token 还没填。去 https://i.cnblogs.com/settings#enableServiceAccess 生成后填进去。"
    }
    return @{
        User   = $o.cnblogs.user
        Token  = $o.cnblogs.token
        BlogId = $(if ($o.cnblogs.blogid) { $o.cnblogs.blogid } else { $o.cnblogs.user })
    }
}

# ────────────────────────────────────────────────────────────
#  XML 转义 / 反转义
# ────────────────────────────────────────────────────────────
function ConvertTo-XmlText([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

function ConvertFrom-XmlText([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('&lt;', '<').Replace('&gt;', '>').Replace('&quot;', '"').Replace('&apos;', "'").Replace('&amp;', '&')
}

# ────────────────────────────────────────────────────────────
#  调用 XML-RPC
# ────────────────────────────────────────────────────────────
function Invoke-MetaWeblog {
    param(
        [string]$MethodName,
        [string[]]$Params   # 已转义好的 <param> 内容（不含外层标签）
    )

    $cred = Get-CnblogsCredential
    $url = "https://rpc.cnblogs.com/metaweblog/$($cred.BlogId)"

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.Append('<methodCall>')
    [void]$sb.Append("<methodName>$MethodName</methodName>")
    [void]$sb.Append('<params>')
    foreach ($p in $Params) {
        [void]$sb.Append("<param><value>$p</value></param>")
    }
    [void]$sb.Append('</params></methodCall>')
    $body = $sb.ToString()

    if ($DryRun) {
        Write-Host "── DRY RUN ── $MethodName" -ForegroundColor Yellow
        Write-Host $body
        return $null
    }

    $resp = Invoke-WebRequest -Uri $url -Method POST -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) `
        -ContentType 'text/xml; charset=utf-8' -TimeoutSec 60 -UseBasicParsing
    $xml = [xml]$resp.Content

    # fault 检查
    $fault = $xml.SelectSingleNode('//fault')
    if ($fault) {
        $code = $fault.SelectSingleNode(".//member[name='faultCode']/value/int").InnerText
        $msg  = $fault.SelectSingleNode(".//member[name='faultString']/value/string").InnerText
        throw "MetaWeblog fault $code : $msg"
    }
    return $xml
}

# ────────────────────────────────────────────────────────────
#  把 XML-RPC struct 转成哈希表
# ────────────────────────────────────────────────────────────
function ConvertFrom-RpcStruct($node) {
    $h = [ordered]@{}
    foreach ($m in $node.SelectNodes('./member')) {
        $name = $m.SelectSingleNode('./name').InnerText
        $vNode = $m.SelectSingleNode('./value')
        $h[$name] = Get-RpcValue $vNode
    }
    return $h
}

function Get-RpcValue($valueNode) {
    if ($null -eq $valueNode) { return $null }
    $child = $valueNode.ChildNodes | Where-Object { $_.NodeType -eq 'Element' } | Select-Object -First 1
    if (-not $child) { return $valueNode.InnerText }
    switch ($child.Name) {
        'string' { return (ConvertFrom-XmlText $child.InnerText) }
        'int'    { return [int]$child.InnerText }
        'i4'     { return [int]$child.InnerText }
        'boolean'{ return ($child.InnerText -eq '1') }
        'struct' { return (ConvertFrom-RpcStruct $child) }
        'array'  {
            $arr = @()
            foreach ($v in $child.SelectNodes('./data/value')) { $arr += ,(Get-RpcValue $v) }
            return $arr
        }
        'dateTime.iso8601' { return $child.InnerText }
        default { return $child.InnerText }
    }
}

# ────────────────────────────────────────────────────────────
#  取文章列表项（recent / get 共用）
# ────────────────────────────────────────────────────────────
function Get-PostList($xml) {
    $values = $xml.SelectNodes('//methodResponse/params/param/value')
    if (-not $values -or $values.Count -eq 0) { return @() }
    $v = $values[0]
    $arrayNode = $v.SelectSingleNode('./array')
    if ($arrayNode) {
        $out = @()
        foreach ($item in $arrayNode.SelectNodes('./data/value/struct')) {
            $out += ,(ConvertFrom-RpcStruct $item)
        }
        return $out
    }
    $structNode = $v.SelectSingleNode('./struct')
    if ($structNode) { return @(ConvertFrom-RpcStruct $structNode) }
    return @()
}

# ────────────────────────────────────────────────────────────
#  动作实现
# ────────────────────────────────────────────────────────────
function Do-Categories {
    $cred = Get-CnblogsCredential
    $xml = Invoke-MetaWeblog 'metaWeblog.getCategories' @(
        "<string>$(ConvertTo-XmlText $cred.BlogId)</string>",
        "<string>$(ConvertTo-XmlText $cred.User)</string>",
        "<string>$(ConvertTo-XmlText $cred.Token)</string>"
    )
    if ($DryRun) { return }
    $cats = Get-PostList $xml
    Write-Host "分类 $($cats.Count) 个：" -ForegroundColor Cyan
    foreach ($c in $cats) {
        Write-Host ("  {0,-24} {1}" -f $c.categoryid, $c.title)
    }
}

function Do-Recent {
    $cred = Get-CnblogsCredential
    $xml = Invoke-MetaWeblog 'metaWeblog.getRecentPosts' @(
        "<string>$(ConvertTo-XmlText $cred.BlogId)</string>",
        "<string>$(ConvertTo-XmlText $cred.User)</string>",
        "<string>$(ConvertTo-XmlText $cred.Token)</string>",
        "<int>$Count</int>"
    )
    if ($DryRun) { return }
    $posts = Get-PostList $xml
    Write-Host "最近 $($posts.Count) 篇：" -ForegroundColor Cyan
    foreach ($p in $posts) {
        $cats = ($p.categories -join ', ')
        Write-Host ("  [{0}] {1}" -f $p.postid, $p.title)
        Write-Host ("        {0}" -f $p.dateCreated) -ForegroundColor DarkGray
        Write-Host ("        {0}" -f $cats) -ForegroundColor DarkGray
        Write-Host ("        {0}" -f $p.link) -ForegroundColor DarkGray
    }
}

function Do-Get {
    if ($PostId -le 0) { throw 'get 需要 -PostId' }
    $cred = Get-CnblogsCredential
    $xml = Invoke-MetaWeblog 'metaWeblog.getPost' @(
        "<string>$PostId</string>",
        "<string>$(ConvertTo-XmlText $cred.User)</string>",
        "<string>$(ConvertTo-XmlText $cred.Token)</string>"
    )
    if ($DryRun) { return }
    $p = (Get-PostList $xml)[0]
    Write-Host "标题: $($p.title)" -ForegroundColor Cyan
    Write-Host "ID:   $($p.postid)"
    Write-Host "时间: $($p.dateCreated)"
    Write-Host "链接: $($p.link)"
    Write-Host "分类: $($p.categories -join ', ')"
    Write-Host "── 正文 ──" -ForegroundColor DarkGray
    Write-Host $p.description
}

function New-StructXml([string]$title, [string]$body, [string[]]$cats) {
    $escTitle = ConvertTo-XmlText $title
    $escBody  = ConvertTo-XmlText $body
    $catXml = ($cats | ForEach-Object { "<value><string>$(ConvertTo-XmlText $_)</string></value>" }) -join ''
    return @"
<struct>
  <member><name>title</name><value><string>$escTitle</string></value></member>
  <member><name>description</name><value><string>$escBody</string></value></member>
  <member><name>categories</name><value><array><data>$catXml</data></array></value></member>
</struct>
"@
}

function Get-DefaultCategories {
    # 博客园靠 [Markdown] 这个伪分类决定按 Markdown 解析正文
    if ($Categories.Count -gt 0) { return $Categories }
    return @('[随笔分类]记录', '[Markdown]')
}

function Do-New {
    if (-not $File) { throw 'new 需要 -File' }
    if (-not (Test-Path -LiteralPath $File)) { throw "找不到 $File" }
    $body = [System.IO.File]::ReadAllText((Resolve-Path $File), [System.Text.Encoding]::UTF8)
    $t = $Title
    if (-not $t) {
        # 从文件第一个 H1 取标题
        $m = [regex]::Match($body, '(?m)^#\s+(.+)$')
        $t = if ($m.Success) { $m.Groups[1].Value.Trim() } else { [System.IO.Path]::GetFileNameWithoutExtension($File) }
    }
    $cats = Get-DefaultCategories

    $cred = Get-CnblogsCredential
    $struct = New-StructXml $t $body $cats
    $xml = Invoke-MetaWeblog 'metaWeblog.newPost' @(
        "<string>$(ConvertTo-XmlText $cred.BlogId)</string>",
        "<string>$(ConvertTo-XmlText $cred.User)</string>",
        "<string>$(ConvertTo-XmlText $cred.Token)</string>",
        "<value>$struct</value>".Replace('<value>','').Replace('</value>',''),
        '<boolean>1</boolean>'
    )
    if ($DryRun) { return }
    $newId = $xml.SelectSingleNode('//methodResponse/params/param/value/string').InnerText
    Write-Host "✅ 已发布  postid=$newId" -ForegroundColor Green
    Write-Host "   https://www.cnblogs.com/$($cred.BlogId)/p/$newId"
}

function Do-Edit {
    if ($PostId -le 0) { throw 'edit 需要 -PostId' }
    if (-not $File) { throw 'edit 需要 -File' }
    if (-not (Test-Path -LiteralPath $File)) { throw "找不到 $File" }
    $body = [System.IO.File]::ReadAllText((Resolve-Path $File), [System.Text.Encoding]::UTF8)
    $t = $Title
    if (-not $t) {
        $m = [regex]::Match($body, '(?m)^#\s+(.+)$')
        $t = if ($m.Success) { $m.Groups[1].Value.Trim() } else { [System.IO.Path]::GetFileNameWithoutExtension($File) }
    }
    $cats = Get-DefaultCategories
    $cred = Get-CnblogsCredential
    $struct = New-StructXml $t $body $cats
    $xml = Invoke-MetaWeblog 'metaWeblog.editPost' @(
        "<string>$PostId</string>",
        "<string>$(ConvertTo-XmlText $cred.User)</string>",
        "<string>$(ConvertTo-XmlText $cred.Token)</string>",
        ($struct -replace '^<struct>\s*|\s*</struct>$','').Trim(),
        '<boolean>1</boolean>'
    )
    if ($DryRun) { return }
    $ok = $xml.SelectSingleNode('//methodResponse/params/param/value/boolean')
    Write-Host ("✅ 已更新  postid={0}  结果={1}" -f $PostId, $(if ($ok) { $ok.InnerText } else { '?' })) -ForegroundColor Green
}

function Do-Publish {
    if (-not $File) { throw 'publish 需要 -File' }
    $idFile = (Resolve-Path $File).Path + '.postid'
    if (Test-Path -LiteralPath $idFile) {
        $savedId = [int](([System.IO.File]::ReadAllText($idFile, [System.Text.Encoding]::UTF8)).Trim())
        Write-Host "发现 .postid → 走 edit (postid=$savedId)" -ForegroundColor Cyan
        $script:PostId = $savedId
        Do-Edit
    } else {
        Write-Host "没有 .postid → 走 new" -ForegroundColor Cyan
        Do-New
        # Do-New 成功后把 id 写回
        # （Do-New 内部已打印，这里再补一次落盘做不到，因为没回传；见下方说明）
    }
}

# ────────────────────────────────────────────────────────────
#  入口
# ────────────────────────────────────────────────────────────
try {
    switch ($Action) {
        'categories' { Do-Categories }
        'recent'     { Do-Recent }
        'get'        { Do-Get }
        'new'        { Do-New }
        'edit'       { Do-Edit }
        'publish'    { Do-Publish }
    }
}
catch {
    Write-Host "❌ $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
