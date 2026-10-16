# Transit 屏幕叠加面板

> **作用**：游戏内 F9 面板/标注/导出
> **影响范围**：客户端 UI
> **状态**：❌ 已从四端移除（备份在 _plugin_backups）
> **部署位置**：—
> **目录**：`工作区\_transitoverlay`

## 源码文件

- `TransitOverlayPlugin.cs`

## 产物 dll（非 obj/bin）

（无）

## 怎么构建

```powershell
cd "C:\Users\Administrator\Desktop\dsh\工作区\_transitoverlay"
dotnet build -c Release
```

产物在 `bin\Release\`。

## 注意

- `obj\` / `bin\` 是构建缓存，不要手动改
- 部署前先确认游戏端/服务端已停止，否则文件被占用
- 本项目 dll 会通过 NarcoNet 自动分发到其它客户端，改动会影响所有人
