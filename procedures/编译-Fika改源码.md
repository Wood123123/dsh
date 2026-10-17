# 编译-Fika改源码

> **触发条件**：想改 Fika 本体（加同步修复、加看门狗），拿官方 tag 源码一编译就 90 个错；
> 或改完了 dll 覆盖不进去。

---

## 首选姿势（可直接抄）

### 1. 拿源码 + 只有 `References\hollowed.dll` 能用官方的

```powershell
git clone --depth 1 --branch v2.4.6 https://github.com/project-fika/Fika-Plugin.git
```

**`References\` 里的 dll 只有一个不能换：`hollowed.dll`（= 裁剪过的 Assembly-CSharp）。**

```powershell
# 除 hollowed.dll 外，其它全用本地的
Copy-Item 'F:\EFT\EscapeFromTarkov_Data\Managed\*\*.dll'   # 逐个映射
Copy-Item 'F:\EFT\BepInEx\plugins\spt\spt-*.dll'
Copy-Item 'F:\EFT\BepInEx\core\0Harmony.dll','BepInEx.dll'
# ★ hollowed.dll 必须留 upstream 的
Copy-Item '.\References_upstream\hollowed.dll' .\References\ -Force
```

### 2. 编译

```powershell
cd Fika.Core
dotnet build -c Release
# 产物：bin\Release\netstandard2.1\Fika.Core.dll
# postbuild 还会拷一份到 Build\BepInEx\plugins\
```

### 3. 部署（进程在跑时放 `.new`）

```powershell
Copy-Item $new $dst.FullName -Force              # 正常情况
# 若报 "user-mapped section open"：
Copy-Item $new "$dst.new" -Force                 # 放 .new，等下次重启改名
```

---

## 反面清单（试过不通的）

| 做法 | 结果 |
|---|---|
| 用本地 `Assembly-CSharp.dll` 当 `hollowed.dll` | ❌ **90 个错**：`_updateQueue` / `_menuOperation` / `_sign` / `string_2` / `World._interactableObjectsForNetSync` 全是 private/internal，原始 dll 下不可访问 |
| 想改 csproj 指向本地程序集 | ❌ 同上，问题是**访问级别**不是版本号 |
| 直接覆盖正在运行的 dll | ❌ `The requested operation cannot be performed on a file with a user-mapped section open` |
| 硬杀进程再覆盖 | ⚠ 可以，但先看日志确认它没在战局里（`LogOutput.log` 末尾是 hideout icon / menus 就是在启动/空闲） |

**关键认知**：`hollowed.dll` 是**裁剪版**（9.27 MB vs 原始 16.2 MB），把 private 全放开了。
所以"本地 EFT 版本一致"不代表能编译 —— **必须用官方的 hollowed.dll**。

---

## EFT 4.1.6 里几个和 Fika 3.0.1（SPT5.0/IL2CPP）不一样的地方

移植 3.x 的代码时要改这些：

| Fika 3.0.1 写法 | EFT 4.1.6 实际 |
|---|---|
| `mc.PlayerAnimatorSetInteraction(false)` | `PlayerAnimatorSetInteraction(bool isInteracting, bool approached)` — **两个参数** |
| `state.Name`（属性） | `BaseMovementState.Name` 是**字段** `EPlayerState Name` |
| `CurrentState is MovementState` | `CurrentState` 是 `BaseMovementState`；`Cancel()` 在 `MovementState` 上，要 `as` 转换 |
| `MovementContext.StateLocksInventory`（读） | **只写属性，没有 getter**，读不了 |
| `controller.Locked` 在 `InventoryController` 上 | 实际在基类 `ItemController.Locked`（`InventoryController ← PersonItemController ← ItemController`） |
| `movementContext.Exit()` | 在 `BaseMovementState.Exit(bool toSameState)` |
| `EPlayerState` 可 null | **值类型**，不能赋 null；用 `EPlayerState.None` 当哨兵 |

**上锁的三个移动状态**（`StateLocksInventory` 的唯一写入方）：
`ApproachState` / `DoorInteractState` / `StationaryPlayerState`
（注意 `StationaryPlayerState` 有 **Enter 和 Spawned 两个**写入点）

---

## 验证方式（怎么确认真成了）

```powershell
# 1. 确认改动进了 dll（指令数变化是最硬的证据）
Add-Type -Path "$env:TEMP\cecil\Mono.Cecil.dll"
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
# 找新类型 / 新方法 / 目标方法的指令数变化

# 2. 确认调用点挂上了
# 搜目标方法体里有没有 call 到你的新方法

# 3. 确认版本号
[System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
```

**实例**：`OnNetworkReceive` 改前 79 指令 → 改后 82 指令（+ldarg/call/brtrue），一眼可见。
