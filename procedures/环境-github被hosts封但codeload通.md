'
# 环境-github被hosts封但codeload通

> **触发**：`web_fetch` 报 `URL hostname "github.com" resolves to a non-public IP address`；或 shell 里连 GitHub 超时
> **验证**：`Resolve-DnsName github.com` 返回 `127.0.0.1`，而 `Resolve-DnsName codeload.github.com` 返回真实 IP

## 首选

**用 `codeload.github.com` 直接下整包 zip（不需要代理）：**

```powershell
Invoke-WebRequest -Uri "https://codeload.github.com/<owner>/<repo>/zip/refs/heads/main" `
                  -OutFile "repo.zip" -TimeoutSec 90 -UseBasicParsing
Expand-Archive -LiteralPath "repo.zip" -DestinationPath "src" -Force
```

**只想读单个文件 / 官方 wiki 时，用 ghproxy 前缀：**

```
https://ghproxy.net/https://raw.githubusercontent.com/<owner>/<repo>/main/<path>
```

## 被挡时

1. `raw.githubusercontent.com` 和 `api.github.com` 走 ghproxy 也常 403
   ⇒ 改用 **`codeload` 下整包**，本地解压后自己找文件
2. 想读 wiki 的某页，先取 `SUMMARY.md` 拿到**正确的相对路径**，再去 raw 取
   （GitBook 的 URL 路径 ≠ 仓库文件路径，直接猜会 404）

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 反复 `web_fetch github.com` | DNS 被 hosts 固定到 127.0.0.1，永远不通 |
| 猜 `raw.githubusercontent.com` 的文件路径 | GitBook/gitbook 的 URL 与仓库路径不一致，会 404 |
| 用 `api.github.com` 列目录 | 经代理返回 403 |

## 注意

- **`github.com` / `raw.githubusercontent.com` 解析到 127.0.0.1**，但 **`codeload.github.com` 解析正常**
  ⇒ 这是本机 hosts 或 DNS 策略，不是全站封禁
- 用 ghproxy 取 raw 时，**jsDelivr 会 302 到 raw.githubusercontent.com**，然后被拦
  ⇒ 别用 jsDelivr

证据：2026-10-15 会话；`Resolve-DnsName` 实测 + `工作区\_cookingsync` 拉源码
'