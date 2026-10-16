# 工作区

> **这里放「活跃项目」** —— 会改、会编译、会部署的东西。
> 上级索引：`..\_索引.md`

## 两类项目

### 📦 独立 git 仓库（已可发布）

这三个是**独立的 git 仓库**，各推各的远程：

| 目录 | 仓库名 | 是什么 | 本地裸仓库 |
|---|---|---|---|
| `headless-panel\` | HeadlessPanel | SPT/Fika 服务端 Web 面板（战绩/日志/审计） | `D:\git\HeadlessPanel.git` |
| `fika-inventory-resync\` | FikaInventoryResync | 修 Fika 联机物品栏失同步 + 残留锁看门狗 | `D:\git\FikaInventoryResync.git` |
| `TacticalSprint-Companions\` | TacticalSprint-Companions | Tactical Sprint 的 Fika同步 / 负重限制 / 速度修正 | `D:\git\TacticalSprint-Companions.git` |

**⇒ 这三个在父仓库的 `.gitignore` 里**（避免 embedded repository）。
**⇒ 推送用 `..\push-all.ps1`。**

`TacticalSprint-Companions\` 的内部结构：
```
src\TacticalSprint.Fika\          联机动画同步（依赖 Fika + Tactical Sprint）
src\TacticalSprint.WeightGate\    负重限制（依赖 Tactical Sprint）
src\TacticalSprint.SpeedFix\      速度修正（无依赖）
TacticalSprint-Companions.sln     一次编三个
```

### 内部项目（未发布）

**部署中：**
```
_cookingsync            手雷烹饪时长联机同步
_shijiufikafix          绕过拾玖 Fika 版本门（修「结果未确认」）
_shijiuhistoryfix       服务端：拾玖战绩重连冲突自动修复
_lootdiag               拾取诊断（只读）
_logshipper             日志上传到面板
_transitprefetch        BTR 传送预取修复
_reconnectbutton        主菜单重连按钮
manimal-audio-gate      Icebreaker 音频门控
shijiu-price-fast       拾玖价格模块（服务端）
```

**已停用/已移除（源码留档）：**
```
_transitdiag        Transit 诊断探针   —— 已从四端移除
_transitoverlay     F9 屏幕面板       —— 已从四端移除
_shijiufix          拾玖重连跳过       —— 从未生效（FindType bug）
_tripwiresyncfix    绊线同步修复       —— 补丁从未挂上，已移除
```

**诊断/辅助：**
```
_busydiag / _headlessdiag / _headlesstelemetry
_kitfix / _tacticalsprint_build / _tripwire_build
```

> ⚠ `_tacticalsprint_build` 和 `_tripwire_build` 是**别人 mod 的构建产物**
> （GUID 分别是 `com.fromhell.tacticalsprint` 和 `ciallo.inftripwire`）—— **不要发布**。

## 规则

- 每个子目录 = **一个独立的源码项目**，内含 `README.md` 说明「作用 / 影响范围 / 状态 / 部署位置」
- 改完代码要 `dotnet build -c Release`，然后按 README 里的「部署位置」拷过去
- **不要在别处另建副本**：所有源码只放一份
- `obj\` / `bin\` 是构建缓存，不要手动改

## 构建通用写法

```powershell
# 客户端插件
dotnet build -c Release -p:EFT_DIR="你的 SPT 安装目录"

# 服务端 mod
dotnet build -c Release -p:SPT_DIR="你的 SPT_Runtime 目录"
```

**⇒ `EFT_DIR` / `SPT_DIR` 都是 csproj 里的可覆盖变量，不要改死路径。**