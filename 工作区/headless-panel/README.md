# HeadlessPanel

**给 SPT / Fika 服务端加一个 Web 面板** —— 战绩记录、日志聚合、Raid Code 分组，浏览器直接看。

> 服务端 mod（不是客户端插件）。装在跑 `SPT.Server.exe` 的那台机器上。

---

## 功能

### ① 战绩记录

按 **Raid Code** 分组展示每一局的战绩。同一局的多个玩家记录会归到一组，一眼看出谁和谁在同一局。

### ② 日志聚合

所有客户端 / 无头端的 BepInEx 日志集中到一个地方看，不用挨个机器去翻 `LogOutput.log`。

- 客户端侧需要一个上传插件（见下方「日志上传」）
- 服务端自己的日志也会被抓取（`ServerLogTailer`）

### ③ 审计日志

记录谁在什么时候访问了面板的哪些接口（`AuditLog`）。

### ④ 实时性

面板是纯静态 HTML + 轮询，不需要 WebSocket，也不需要单独起服务 —— **挂在 SPT 服务端自己的 HTTP 端口上**（默认 6969）。

---

## 安装

1. 需要 **SPT 4.1.6** 或兼容版本
2. 把 `HeadlessPanel.dll` + `HeadlessPanel.deps.json` + `wwwroot\` 放进：
   ```
   <SPT根>\SPT_Runtime\user\mods\HeadlessPanel\
   ```
3. 重启服务端

**目录结构应该是：**
```
SPT_Runtime\user\mods\HeadlessPanel\
  HeadlessPanel.dll
  HeadlessPanel.deps.json
  wwwroot\
    index.html
```

4. 浏览器打开 `https://<服务端地址>:6969/headlesspanel`

> SPT 用的是自签 HTTPS 证书，浏览器会警告 —— 正常，点「继续访问」。

---

## 日志上传（可选，但推荐）

光装面板只能看到**服务端自己**的日志。要看到各个客户端 / 无头端的日志，
需要在每个客户端装一个上传插件，把 `LogOutput.log` 推到面板的接口：

```
POST /api/logs/ingest
```

> 上传插件的接口约定：增量推送，带时间戳和来源标识。

---

## 配置

面板会在 mod 目录下生成配置文件。主要项：

| 项 | 说明 |
|---|---|
| 监听路径 | 面板挂载的 URL 前缀，默认 `/headlesspanel` |
| 日志保留 | 日志聚合保留多久 / 多少条 |
| Fika 地址 | 面板查询 Fika 服务端用的地址，默认 `https://127.0.0.1:6969` |

---

## 构建

```powershell
dotnet build -c Release -p:SPT_DIR="你的 SPT_Runtime 目录"
```

`SPT_DIR` 默认 `F:\EFT\SPT_Runtime`（见 csproj 里的 `$(SPT_DIR)`）。

**产物：** `bin\Release\HeadlessPanel.dll` + `HeadlessPanel.deps.json` + `wwwroot\`

**直接部署到服务端**（省得手动拷）：
```powershell
dotnet build -c Release -p:SPT_DIR="D:\SPT\SPT_Runtime" -p:DeployToGame=true
```

> 目标框架是 **net10.0**，需要 .NET 10 SDK。
> 用 `FrameworkReference Include="Microsoft.AspNetCore.App"`，所以不需要额外引 ASP.NET Core 包。

---

## 技术说明

```
HeadlessPanelController.cs   控制器 —— 所有 HTTP 接口
LogStore.cs                  日志存储与检索（63 KB，最核心）
ServerLogTailer.cs           服务端日志实时抓取
AuditLog.cs                  访问审计
HeadlessPanelConfig.cs       配置
FikaBridge.cs                与 Fika 服务端的桥接
HeadlessPanelMetadata.cs     IModMetadata 入口
wwwroot\index.html           前端（单文件，37 KB）
```

- **依赖注入**：用 SPT 的 `[Injectable]` 体系，不自己造容器
- **前端零依赖**：单文件 HTML，没有 npm / 打包步骤
- **不额外起端口**：复用 SPT 服务端的 HTTP 端口

---

## 已知问题

- **只支持单服务端**：没有多实例聚合
- **轮询而非推送**：页面刷新有延迟（取决于轮询间隔）
- **审计是记录不是防护**：只写日志，不做访问控制

---

## 许可

MIT