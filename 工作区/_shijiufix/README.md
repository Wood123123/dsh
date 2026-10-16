# 拾玖重连跳过（已废弃）

> **作用**：原意：重连时跳过 FikaHistory.Capture
> **影响范围**：客户端
> **状态**：❌ 已停用（FindType 用全名比简单名，从未生效）
> **部署位置**：—
> **目录**：`工作区\_shijiufix`

## 源码文件

- `ShijiuReconnectFixPlugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_shijiufix"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
