# 环境-curl和HttpWebRequest不走本地加速器

> **触发**：`curl.exe` 或 .NET 的 `HttpWebRequest` 访问某个域名返回 **HTTP 000** 或连不上，
> 但同一时刻 PowerShell 的 `Invoke-RestMethod` / `Invoke-WebRequest` 却能成功
> **验证**：`Invoke-WebRequest -Uri <同URL> -Method Head` 返回 200

## 首选

**诊断和 API 调用一律用 `Invoke-RestMethod` / `Invoke-WebRequest`，不要用 curl.exe。**

```powershell
# ✓ 会走系统代理链，本地加速器能接管
Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers @{
    Authorization = "token $tok"
    Accept        = 'application/vnd.github+json'
    'User-Agent'  = 'app/1.0'
}

# ✗ 不走，直接连 hosts 里的 127.0.0.1
curl.exe https://api.github.com/user
```

**只想拿状态码 / 响应头**（curl 的 `-w`/`-D` 替代）：

```powershell
$req = [System.Net.HttpWebRequest]::Create($url)
$req.UserAgent = 'app/1.0'                 # ★ 必须用属性，不能 Headers.Add
$req.Accept    = 'application/json'        # ★ 同上
$req.Headers.Add('Authorization', "token $tok")   # 这个可以用 Add
try { $r = $req.GetResponse(); $code = [int]$r.StatusCode; $r.Headers.AllKeys }
catch [System.Net.WebException] { $r = $_.Exception.Response; $code = [int]$r.StatusCode; $r.Headers.AllKeys }
```

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 用 `curl.exe` 做连通性探测 | 它走自己的 DNS 解析，`hosts` 把域名指到 `127.0.0.1` 就连到本地了，返回 000 |
| 用 `$req.Headers.Add('User-Agent', ...)` | .NET 抛 `ArgumentException`：`User-Agent`/`Accept` 是受限头，必须用**属性**赋值 |
| 看到 000 就以为"网络断了" | 同时刻 `Invoke-RestMethod` 是通的 —— 是工具链差异，不是网络 |

## 注意

- **根因**：本机 hosts 被加速器（Steam++ / Watt Toolkit）写入大量 `127.0.0.1` 条目，
  加速器自己在 `0.0.0.0:80` 和 `0.0.0.0:443` 监听并反代回真实站点。
  - **走系统代理链的程序**（PowerShell 的 IWR / IRM、浏览器）→ 被加速器接管 → 通
  - **自己解析 DNS 的程序**（curl、部分 .NET API）→ 连到 127.0.0.1 → 不通
- **判据**：先跑一次 `Invoke-WebRequest -Method Head`。它通而 curl 不通 ⇒ 就是这个坑
- **加速器一停，hosts 还指着 127.0.0.1**，所有走 hosts 的程序全断。
  关加速器前先在它界面里点「解除 hosts 占用」

证据：2026-10-16 会话；`curl -w '%{http_code}'` 返回 000 而 IWR 返回 200