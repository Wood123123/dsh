# 拾玖 Fika 版本门绕过

> **作用**：绕过 ShiJiu-Tools.Fika 的硬版本检查（只认 Fika 2.4.4），强制 FikaHistory.Install() 并每帧 Tick
> **影响范围**：客户端；影响对局战绩记录能否正常结束
> **状态**：✅ 部署中（SHA 5523D480AAB8）
> **部署位置**：Wood 客户端 + 分发源
> **目录**：`工作区\_shijiufikafix`

## 源码文件

- `ShijiuFikaFixPlugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_shijiufikafix"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
