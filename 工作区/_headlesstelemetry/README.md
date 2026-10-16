# 无头端遥测

> **作用**：采集无头端 FPS/内存/趋势并写本地文件
> **影响范围**：仅诊断
> **状态**：✅ 部署中
> **部署位置**：无头端
> **目录**：`工作区\_headlesstelemetry`

## 源码文件

- `HeadlessTelemetryPlugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_headlesstelemetry"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
