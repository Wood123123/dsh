# CookingGrenades 联机烹饪同步

> **作用**：让手雷"烹饪时长"在 Fika 联机中同步：挂钩 ThrowGrenade/Serialize/ctor/Execute/SpawnGrenade
> **影响范围**：客户端 + 无头端；影响所有人看到的手雷引信时间
> **状态**：✅ 部署中（SHA 6D7EB5DBEAA4）
> **部署位置**：四端 + 分发源
> **目录**：`工作区\_cookingsync`

## 源码文件

- `CookingFikaSyncPlugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_cookingsync"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
