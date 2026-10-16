# 物品忙碌诊断

> **作用**：追踪 ItemController._activeEvents，定位"正在被修改"错误
> **影响范围**：仅诊断
> **状态**：诊断用
> **部署位置**：按需
> **目录**：`工作区\_busydiag`

## 源码文件

- `BusyDiag.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_busydiag"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
