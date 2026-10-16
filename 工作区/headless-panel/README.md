# HeadlessPanel

**给 SPT / Fika 服务端加一个「无头端管理面板」** —— 浏览器里监控、启动、重启无头客户端，
顺带看战绩、聚合所有端的日志、审计访问记录。

> 服务端 mod（不是客户端插件）。装在跑 `SPT.Server.exe` 的那台机器上。
> **SPT 4.1.x · v1.0.0**

---

## 功能

### ① ★ 无头端进程管理

| 操作 | 说明 |
|---|---|
| **启动** | 按配置的 `managerPath` 拉起无头端（带启动冷却，默认 180s） |
| **停止** | 结束进程 |
| **重启** | 走 Fika 的 HTTP API（那条路径掌握无头端监听的关闭消息），apiKey 留在服务端 |
| **列表** | 一行一个无头端，状态实时合并三方信息 |

**状态判定**（`connected` / `starting` / `stuck` / `stopped` / `ready` / `inraid`）：

```
进程起来了 + Fika 已接受它的连接   → ready（无人）/ inraid（有人）
进程起来了 + Fika 还没接受连接     → starting
starting 超过 300 秒               → stuck   ← 几乎总是「管理器已死、游戏进程成孤儿」
进程不在                          → stopped
```

### ② 玩家列表

`/api/players` —— 当前谁在线。

### ③ 战绩记录

`/api/raids` 列局，`/api/raid` 看单局。同一局的多个玩家记录会归到一组。

### ④ 日志聚合

所有客户端 / 无头端的 BepInEx 日志集中到一个地方，不用挨个机器翻 `LogOutput.log`。

- `/api/logs/raids` —— 按局分组
- `/api/logs/timeline` —— 时间线
- `/api/logs/prune` —— 清理
- `/api/logs/ingest` —— **客户端上报入口**（见下）

### ⑤ 遥测

`/api/telemetry` —— 无头端的 FPS / 内存等指标。

### ⑥ 访问审计

`/api/audit`，落盘到 `user\logs\HeadlessPanel\audit.jsonl`。

---

## 安装

1. 需要 **SPT 4.1.x**
2. 把 `HeadlessPanel.dll` + `HeadlessPanel.deps.json` + `wwwroot\` 放进：
   ```
   <SPT根>\SPT_Runtime\user\mods\HeadlessPanel\
   ```
3. 重启服务端
4. 打开 **`https://<服务端地址>:6969/headlesspanel`**

> ⚠ SPT 用自签 HTTPS 证书，浏览器会警告 —— 正常，点「继续访问」。

**★ 认证**：路由挂了 `[Authorize]`，复用 **SPT 自带 Web UI 的 cookie 登录**
（就是 `/login` 那个页面）。所以你得先能登录 SPT 的 Web 界面。

**目录结构：**
```
SPT_Runtime\user\mods\HeadlessPanel\
  HeadlessPanel.dll
  HeadlessPanel.deps.json
  wwwroot\
    index.html
  config.json          ← 首次启动自动生成
```

---

## 配置

`<SPT根>\SPT_Runtime\user\mods\HeadlessPanel\config.json`

**（改完 5 秒内自动生效，不用重启服务端。）**

```jsonc
{
  // 每个无头端一条
  "headlessInstances": [
    {
      "name":             "无头端0",
      "profileId":        "6a97214f53af47255441a827",
      "managerPath":      "C:\\...\\FikaHeadlessManager.exe",   // 按「启动」时拉起的东西
      "workingDirectory": "C:\\...\\EFT-headless",              // 进程工作目录；也用来识别"起来了但没连上"
      "enabled":          true                                   // false = 可见但不可启动
    }
  ],

  "startCooldownSeconds": 180,     // 同一实例两次「启动」之间的最短间隔
  "allowStart":           true,    // false 就隐藏启动/重启按钮

  "logIngestToken":       "",      // 见「日志上传」
  "serverLogFilter":      "NoPanel",  // NoPanel | NoHttp | WarnPlus | All
  "serverLogTimestampIsUtc": true,    // SPT 服务端日志行内时间戳是 UTC（实测确认）
  "serverLogGraceSeconds":   60       // 战局结束后继续采集这么多秒（结算请求紧跟结束发生）
}
```

### `serverLogFilter` 四档

| 值 | 保留什么 |
|---|---|
| **`NoPanel`**（默认） | 丢弃面板自己的轮询请求 |
| `NoHttp` | 再丢弃所有 `[客户端请求]` 行 |
| `WarnPlus` | 只保留 Warning / Error / Fatal |
| `All` | 全留 |

---

## 日志上传（客户端侧）

光装面板只能看到**服务端自己**的日志（面板跑在服务端进程里，直接 tail）。
要看到各客户端 / 无头端的日志，需要每个客户端装一个上传插件，推到：

```
POST /headlesspanel/api/logs/ingest
```

**★ 零配置设计：** `logIngestToken` **默认留空 = 该端点开放**，
所以客户端**不需要拷 token、不需要改任何文件**，装上就上报。

**为什么这样是安全的：**
```
· ingest 只能【追加日志行】
· 它不能控制无头端 —— 那些端点仍然要求 SPT 登录
```

**想收紧**：在 `logIngestToken` 填一个值，**并且**在每个客户端的 `com.dsh.logshipper.cfg` 里填同一个值。

---

## 技术说明

```
HeadlessPanelController.cs   (46 KB)  所有 HTTP 接口
LogStore.cs                  (64 KB)  日志存储与检索 —— 最核心
ServerLogTailer.cs           (10 KB)  服务端日志实时抓取
AuditLog.cs                  (9 KB)   访问审计
FikaBridge.cs                (6 KB)   进程内读 Fika 状态
HeadlessPanelConfig.cs       (6 KB)   配置
HeadlessPanelMetadata.cs     (2 KB)   IModMetadata / IModBlazorMetadata 入口
wwwroot\index.html           (38 KB)  前端（单文件，零依赖）
```

### 三个设计要点

**① 实时状态走进程内，不走 HTTP**

`FikaBridge` 直接在服务端进程里读 Fika 的实时状态 —— **不需要 apiKey、不发 HTTP**，
也**不碰 Fika 的 profile 缓存**（那个缓存在两个 PMC 昵称相同时每次刷新都刷一条警告）。

**② 只有「重启」还走 Fika 的 HTTP API**

因为那条路径掌握无头端监听的关闭消息。**apiKey 从 `fika.jsonc` 读，留在服务端。**

**③ 服务端日志不需要插件上报**

面板跑在服务端进程里，能直接 tail 自己的日志。跨机器的（客户端/无头端）才要插件。

### 前端

单文件 `wwwroot\index.html`，**没有 npm、没有打包步骤**。通过 `IModBlazorMetadata`
注册为 SPT Web UI 的一个页面，`HomePage = "headlesspanel"`。

---

## 构建

```powershell
dotnet build -c Release -p:SPT_DIR="你的 SPT_Runtime 目录"
```

`SPT_DIR` 默认 `C:\SPT\SPT_Runtime`（csproj 里 `$(SPT_DIR)` 的默认值），
必须指向**含 `SPTarkov.Server.Core.dll` 的目录**；指错会立刻报一条清楚的错误。

**直接编译+部署到服务端**（省得手动拷）：
```powershell
dotnet build -c Release -p:SPT_DIR="D:\SPT\SPT_Runtime" -p:DeployToGame=true
```

**目标框架 `net10.0`，需要 .NET 10 SDK。**
用 `FrameworkReference Include="Microsoft.AspNetCore.App"`，所以不需要额外引 ASP.NET Core 包。

---

## 已知问题

- **单服务端**：没有多实例聚合
- **轮询而非推送**：页面刷新有延迟（取决于轮询间隔）
- **审计是记录不是防护**：只写日志，不做访问控制（访问控制靠 SPT 的 `[Authorize]`）
- **「启动」依赖 `managerPath` 配对**：配错了会拉起失败，面板只能报状态
- **`stuck` 判定是启发式**：300 秒阈值硬编码，慢机器上可能误报

---

## English (brief)

**HeadlessPanel** adds a web panel to the SPT/Fika server for **managing headless clients** —
monitor, start, stop and restart them from a browser — plus raid records, aggregated logs
from every end, telemetry and an access audit trail.

Installs into `<SPT root>\SPT_Runtime\user\mods\HeadlessPanel\`. Open
`https://<server>:6969/headlesspanel`. Auth reuses SPT's own web-UI cookie login (`[Authorize]`).

Live state is read **in-process** from Fika (`FikaBridge`) — no HTTP, no apiKey, no touching
Fika's profile cache. Only *restart* still goes over Fika's HTTP API, because that path owns the
shutdown message the headless client listens for.

Config lives at `user\mods\HeadlessPanel\config.json` and is re-read every 5 seconds, so edits
apply without a server restart. The log-ingest endpoint is `[AllowAnonymous]` with an **optional**
token (empty by default) so every client can report logs with zero configuration — it can only
append log lines and cannot control the headless clients.

Build: `dotnet build -c Release -p:SPT_DIR="<SPT_Runtime>"` (needs .NET 10 SDK).

---

## 许可

MIT