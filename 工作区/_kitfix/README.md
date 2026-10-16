# 套装修复

> **作用**：修复装备套装相关行为
> **影响范围**：客户端
> **状态**：待查
> **部署位置**：—
> **目录**：`工作区\_kitfix`

## 源码文件

- `KitFix.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_kitfix"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
