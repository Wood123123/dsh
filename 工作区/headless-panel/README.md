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
| **停止** | 结束该实例的进程（先杀管理器，防止它再把游戏拉起来） |
| **重启** | 走 Fika 的 HTTP API（那条路径掌握无头端监听的关闭消息），apiKey 留在服务端 |
| **列表** | 一行一个无头端，状态实时合并三方信息 |

**状态判定：**

```
进程起来了 + Fika 已接受它的连接   → ready（无人）/ inraid（有人）
进程起来了 + Fika 还没接受连接     → starting
starting 超过 300 秒               → stuck   ← 几乎总是「管理器已死、游戏进程成孤儿」
进程不在                          → stopped
```

### ② 玩家列表

当前谁在线、在哪张图。

### ③ 战绩记录

列局 / 看单局。同一局的多个玩家记录归到一组。

### ④ 日志聚合

所有客户端 / 无头端的 BepInEx 日志集中到一处，不用挨个机器翻 `LogOutput.log`。
按局分组、时间线视图、保留上限、手动清理。

### ⑤ 遥测

无头端的 FPS / 内存等指标，带趋势窗口与陈旧检测。

### ⑥ 访问审计

谁在什么时候调了哪个接口，落盘到 `user\logs\HeadlessPanel\audit.jsonl`。

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

**认证**：路由挂了 `[Authorize]`，复用 **SPT 自带 Web UI 的 cookie 登录**
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

## ★ 无头端是怎么被识别的

**这是最容易踩坑的地方，先说清楚。**

### 三方合并

面板**不做自动发现**。列表的每一行都源自 `config.json` 里手填的一条，
然后为它补充实时状态：

```
① 配置文件      headlessInstances[]  →  决定「有哪些实例」
                name / profileId / managerPath / workingDirectory / enabled

② Fika 上报     反射读 FikaServer.Services.Headless.HeadlessService.HeadlessClients
                （ConcurrentDictionary<profileId, HeadlessClient>）
                ★ 用 instance.ProfileId 当 key 去查
                → 决定「连上了没 / 几个玩家 / 等级 / socket 状态」

③ 进程表        Process.GetProcessesByName("FikaHeadlessManager" / "EscapeFromTarkov")
                ★ 用 workingDirectory 做路径前缀匹配判断归属
                → 决定「进程在不在跑」
```

| 字段 | 干什么用 | 写错会怎样 |
|---|---|---|
| **`profileId`** | **主键**。去 Fika 的字典里查这个无头端 | 查不到 ⇒ 永远显示「已停止」，即使它好好连着 |
| **`workingDirectory`** | 判断哪些进程属于这个实例 | 匹配不上 ⇒ 进程在跑也显示「已停止」；**`关闭` 会什么都杀不掉** |
| **`managerPath`** | 按「启动」时拉起的东西 | 找不到 ⇒ 启动时报「找不到启动器」 |
| `name` | 只用来显示 | 无影响 |
| `enabled` | `false` = 可见但不可启动 | — |

### 没配置的实例也能「被看到」

有一层**孤儿兜底**：凡是从 Fika 报上来、但不在 `config.json` 里的，
面板仍会给它一行，标成 **(未配置)**。

| 无头端的情况 | 会显示吗 | 能操作吗 |
|---|---|---|
| **已连上 Fika** | ✅ 会 —— 显示「(未配置)」，状态 / 玩家数 / 等级 / socket 都是真的 | 启动 ❌ · **重启 ✅** · 关闭 ⚠️ |
| 进程在跑但没连上 | ❌ 不会 —— 进程归属要靠 `workingDirectory`，没配就无从判断 | — |
| 根本没在跑 | ❌ 不会 —— 面板不知道它的存在 | — |

**为什么「重启」行而「关闭」不行？** 三个端点要求不同：

| 端点 | 配置里的实例是必须的吗 |
|---|---|
| 启动 | ✅ 必须 —— 要 `managerPath` 才能拉起进程 |
| 关闭 | ✅ 必须 —— 要 `workingDirectory` 才知道杀哪些进程 |
| **重启** | ❌ **不需要** —— 只把 `{profileId}` 转发给 Fika 的 `/fika/api/restartheadless` |

> ⚠️ **已知瑕疵**：孤儿行的「关闭」按钮是亮的，但点了必然返回 404。应当按 `enabled` 一起禁用。

### 能力边界

```
✅ 看到正在连着的无头端      ← 自动，不需要配置
✅ 重启它们                 ← 也不需要配置
❌ 启动任何无头端            ← 必须配 managerPath
❌ 关闭它们                 ← 必须配 workingDirectory
❌ 看到没启动的无头端         ← 不可能，面板不知道它存在
```

---

## 部署到别人的服务器时

面板**没有任何「添加实例」的界面** —— 只能改文件。

### 第 1 步：装 mod、启动一次服务端

首次启动会在 mod 目录自动生成一份 `config.json`，`headlessInstances` 是**空数组**。

### 第 2 步：填 `headlessInstances`

路径：`<SPT根>\SPT_Runtime\user\mods\HeadlessPanel\config.json`

```jsonc
{
  "headlessInstances": [
    {
      "name":             "无头端0",                                  // 只影响显示
      "profileId":        "6a97214f53af47255441a827",                // ★ 24 位十六进制
      "managerPath":      "D:\\EFT-headless\\FikaHeadlessManager.exe",
      "workingDirectory": "D:\\EFT-headless",                        // ★ 无尾斜杠更稳妥
      "enabled":          true
    }
  ],
  "allowStart": true,
  "startCooldownSeconds": 180
}
```

**`profileId` 从哪拿？**

| 来源 | 位置 |
|---|---|
| **`HeadlessConfig.json`** | 无头端安装根目录，最直接 |
| `fika.jsonc` | 服务端 `user\mods\Fika\` 下的 `headless.profiles.aliases`，key 就是 profileId |
| Fika 的 Web API | 无头端连接后由 Fika 上报 |

**`workingDirectory` 填什么？** 无头端安装的**根目录**（含 `EscapeFromTarkov.exe` 和 `BepInEx` 的那一层），
**不是** `BepInEx\plugins` 那种子目录。

### 第 3 步：验证

看那一行的状态：

```
「就绪」     ⇒ profileId 对了，无头端连着
「战局中」   ⇒ 同上，而且有人在打
「启动中」   ⇒ 进程在跑但 Fika 还没接受连接（正常要等 2-3 分钟）
「启动异常」 ⇒ 超过 300 秒还没连上 —— 几乎总是管理器死了、游戏进程成孤儿
「已停止」   ⇒ 进程不在；或者 profileId / workingDirectory 写错了
```

**⚠️ 判断「已停止」是真停还是配错了**：去任务管理器看有没有 `FikaHeadlessManager` / `EscapeFromTarkov`。
有进程但面板显示「已停止」⇒ **一定是 `workingDirectory` 写错了**。

### 一个提醒

`profileId` 和 `workingDirectory` 写错**不会报错**，只会静默失灵。
配完**一定要确认那一行的状态跟实际相符**。

---

## 配置

`<SPT根>\SPT_Runtime\user\mods\HeadlessPanel\config.json`

**（改完 5 秒内自动生效，不用重启服务端。）**

```jsonc
{
  "headlessInstances": [],          // 见上一节

  "startCooldownSeconds": 180,      // 同一实例两次「启动」之间的最短间隔
  "allowStart":           true,     // false 就隐藏启动/重启按钮

  "logIngestToken":       "",       // 见「日志上传」
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

### 哪些功能需要客户端插件

| 功能 | 需要插件吗 | 数据从哪来 |
|---|---|---|
| 无头端管理 / 列表 / 玩家 / 审计 | ❌ 不需要 | Fika 进程内 + 进程表 |
| 服务端自己的日志 | ❌ 不需要 | 直接 tail |
| **遥测** | ✅ 需要 | `<无头端>\BepInEx\config\headless-telemetry\live.json` |
| **战绩** | ✅ 需要 | `<无头端>\BepInEx\config\headless-telemetry\raids\<实例>\*.json` |
| **客户端/无头端的日志** | ✅ 需要 | 插件 POST 到 `logs/ingest` |

**⇒ 不装任何插件时，面板只剩「管理 + 玩家 + 审计 + 服务端日志」。**

---

## 技术说明

```
HeadlessPanelController.cs   (46 KB)  所有 HTTP 接口（15 个路由）
LogStore.cs                  (64 KB)  日志存储与检索 —— 最核心
ServerLogTailer.cs           (10 KB)  服务端日志实时抓取
AuditLog.cs                  (9 KB)   访问审计
FikaBridge.cs                (6 KB)   进程内读 Fika 状态
HeadlessPanelConfig.cs       (6 KB)   配置
HeadlessPanelMetadata.cs     (2 KB)   IModMetadata / IModBlazorMetadata 入口
wwwroot\index.html           (38 KB)  前端（单文件，零依赖）
```

### HTTP 路由

```
GET  /headlesspanel                     主页面
GET  /headlesspanel/api/list            无头端列表（含孤儿兜底）
GET  /headlesspanel/api/players         在线玩家
POST /headlesspanel/api/start           启动
POST /headlesspanel/api/stop            关闭
POST /headlesspanel/api/restart         重启
GET  /headlesspanel/api/audit           审计记录
GET  /headlesspanel/api/telemetry       遥测
GET  /headlesspanel/api/raids           战绩列表
GET  /headlesspanel/api/raid            单局战绩
POST /headlesspanel/api/logs/ingest     日志上报（[AllowAnonymous]）
GET  /headlesspanel/api/logs/raids      日志按局分组
GET  /headlesspanel/api/logs/timeline   日志时间线
GET  /headlesspanel/api/logs/token      日志令牌
POST /headlesspanel/api/logs/prune      日志清理
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
- **没有「添加实例」界面**：只能改 `config.json`
- **孤儿行的「关闭」按钮会 404**：应按 `enabled` 一起禁用
- **`profileId` / `workingDirectory` 写错静默失灵**：不报错，只显示错误状态
- **轮询而非推送**：页面刷新有延迟
- **审计是记录不是防护**：访问控制靠 SPT 的 `[Authorize]`
- **`stuck` 判定是启发式**：300 秒阈值硬编码，慢机器上可能误报

---

## English (brief)

**HeadlessPanel** adds a web panel to the SPT/Fika server for **managing headless clients** —
monitor, start, stop and restart them from a browser — plus raid records, aggregated logs
from every end, telemetry and an access audit trail.

Installs into `<SPT root>\SPT_Runtime\user\mods\HeadlessPanel\`. Open
`https://<server>:6969/headlesspanel`. Auth reuses SPT's own web-UI cookie login (`[Authorize]`).

**There is no auto-discovery.** Each row comes from a hand-written `headlessInstances[]` entry in
`config.json`, enriched with live state from two sources: `FikaBridge` reads Fika's
`HeadlessService.HeadlessClients` **in-process** by `profileId`, and the process table is matched
by `workingDirectory` prefix. Rows for instances Fika reports but the config does not know about
are still shown, labelled **(未配置 / unconfigured)** — restartable, but not startable or stoppable.

Config lives at `user\mods\HeadlessPanel\config.json` and is re-read every 5 seconds, so edits
apply without a server restart. The log-ingest endpoint is `[AllowAnonymous]` with an **optional**
token (empty by default) so every client can report logs with zero configuration — it can only
append log lines and cannot control the headless clients.

Build: `dotnet build -c Release -p:SPT_DIR="<SPT_Runtime>"` (needs .NET 10 SDK).

---

## 许可

MIT
## 日志保留策略

战局日志（时间线的数据源）按两条规则清理，任一超出即删：

| 规则 | 阈值 | 说明 |
| --- | --- | --- |
| **按时间** | 14 天 | `LogStore.Retention` |
| **按局数** | **最新 20 局** | `LogStore.MaxRaidGroups`，超出后**整局**删（一局含多实例多段） |

- **自动触发**：每局结束（`CloseSegment`）后按需清理一次，节流 10 分钟、后台线程执行，不阻塞关段路径
- **手动触发**：`POST /headlesspanel/api/logs/prune`
- **"一局"的判定**：复用 `LogStore.BuildRaids` —— 它按 RaidCode / 地图 / 时间把断线重连的多个段归为同一局，是唯一权威；不按目录时间硬切
- **正在写入的段不会被删**（避免 Flush 把它重建出来留下残片）
- `#idle#` 段不算"一局"，仍只受 14 天规则约束