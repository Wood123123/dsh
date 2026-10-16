# Transit 诊断探针

> **作用**：抓 transit 相关方法调用
> **影响范围**：仅诊断
> **状态**：❌ 已从四端移除（备份在 _plugin_backups）
> **部署位置**：—
> **目录**：`工作区\_transitdiag`

## 源码文件

- `Plugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_transitdiag"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
