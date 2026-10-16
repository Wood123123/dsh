# FikaInventoryResync

> **作用**：库存失同步时自动请求主机重发；含 LockFix 清理卡住的库存锁
> **影响范围**：客户端 + 无头端；影响拾取/切手卡顿
> **状态**：✅ 部署中（SHA 184A02D0D56C）
> **部署位置**：四端 + 分发源
> **目录**：`工作区\fika-inventory-resync`

## 源码文件

- `FikaInventoryResync.cs`

## 产物 dll（非 obj/bin）

- FikaInventoryResync.顶层副本.dll（19456 B，10-08 15:58）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\fika-inventory-resync"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
