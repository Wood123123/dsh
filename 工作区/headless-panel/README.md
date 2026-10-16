# HeadlessPanel 面板

> **作用**：服务端 ASP.NET 面板：战绩记录、日志聚合、Raid Code 分组
> **影响范围**：服务端；所有客户端看同一个面板
> **状态**：✅ 部署中（DLL CC9B4D34FDA7 / index 933C6BB403D6）
> **部署位置**：SPT_Runtime\user\mods\HeadlessPanel
> **目录**：`工作区\headless-panel`

## 源码文件

- `AuditLog.cs`
- `FikaBridge.cs`
- `HeadlessPanelConfig.cs`
- `HeadlessPanelController.cs`
- `HeadlessPanelMetadata.cs`
- `LogStore.cs`
- `ServerLogTailer.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\headless-panel"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
