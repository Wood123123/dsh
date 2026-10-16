# 发布-fine-grained token 建不了仓库

> **触发**：用 `github_pat_` 开头的 token 调 `POST /user/repos` 建库，返回 403
> **验证**：读 403 的响应头 `x-accepted-github-permissions` —— 它会直接列出缺哪几个权限

## 首选

**建库这种一次性操作，用 classic token（`ghp_`）最省事：**

```
https://github.com/settings/tokens/new
→ Note: 随便
→ ★ 只勾 【repo】 这一整项（会自动带上 5 个子项）
→ Generate token
```

拿到 `ghp_...` 后：

```powershell
$h = @{ Authorization = "token $tok"; Accept = 'application/vnd.github+json'; 'User-Agent' = 'app/1.0' }
Invoke-RestMethod -Uri 'https://api.github.com/user/repos' -Method Post -Headers $h `
  -Body ([Text.Encoding]::UTF8.GetBytes('{"name":"x","private":true,"auto_init":false}')) `
  -ContentType 'application/json'
```

## 被挡时

1. **手动在网页建库**，然后只让 token 负责推送（推送只需 `Contents: Read and write`）
2. 改 fine-grained token 的权限：加 `Administration: Read and write`

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 给 fine-grained token 只勾 repo 访问权就以为能建库 | 建库要 `administration=write` + `repository_creation=write`，这两个默认都没有 |
| 看到 403 就以为 token 无效 | token 完全有效 —— `GET /user` 能过，只是缺那一个动作的权限 |
| 用 `-Headers @{...}` 但不带 User-Agent | 会撞上加速器的规则，返回的是加速器的 403（见另一张卡） |

## 注意

- **两个 403 长得一样、根因完全不同**，必须看响应头区分：
  ```
  Server: github.com,WattToolkit
  x-accepted-github-permissions: administration=write; repository_creation=write
  ```
  有 `x-accepted-github-permissions` ⇒ **GitHub 在说"你缺权限"**
  响应体是 `Request forbidden by administrative rules` ⇒ **是本地加速器在拦**
- **classic token 的权限是"全有全无"**：勾了 `repo` 就能读写你所有仓库。
  用完建议撤销或换更小的 scope
- **读身份用 `Authorization: token <t>` 或 `Bearer <t>` 都能过**，但 classic 更常见 `token`

证据：2026-10-16 会话；`x-accepted-github-permissions` 实测响应头