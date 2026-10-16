# Fika Inventory Resync

**修复 Fika 联机中的物品栏不同步** —— 客户端发现自己的库存和主机对不上时，自动请求主机重发。
附带一个**残留锁看门狗**，解决「装备手雷失败 / 手部卡在空手 / 所有物品操作报 *currently being modified*」的问题。

[English](#english) | 中文

---

## 解决什么问题

Fika 联机里有两类看起来很像、实际根因完全不同的「库存坏了」：

### ① 库存数据失同步

拾取、切枪、换弹时，客户端的 `InventoryController` 和主机的不一致。表现为：

- 捡了东西但背包里没有
- 东西在背包里但用不了
- 别人的视角里你手里是枪，你自己看是空手

**这类问题可以靠「请求主机重发库存」修好。**

### ② 残留物品栏锁（★ 更隐蔽）

游戏里有个运行时标志 `InventoryController.Locked`。只有三个移动状态会设它：

```
ApproachState        （接近交互物）
DoorInteractState    （开门）
StationaryPlayerState（原地蹲/趴）
```

这三者的 `Exit` 里会把它设回 `false`。**但如果交互被打断（掉线、切图、死亡动画、主机卡顿），`Exit` 没被调用**，锁就一直挂着。

挂着的后果：`ItemController.CheckItemAction` 会抛 `PlayerIsBusyError`：

```
Cannot apply {item} because {target} is currently being modified
```

⇒ **该玩家之后的一切物品操作全部失败，直到本局结束。**

**⚠ 这个锁【不会】被「重发库存」修掉** —— 重发换的是 inventory 数据，锁是 `InventoryController` 上的运行时标志，两回事。

### 本插件同时处理这两类

---

## 安装

1. 需要 **SPT 4.1.x** + **[Fika](https://github.com/project-fika/Fika-Plugin)**（已在 Fika.Core 2.4.4 / 2.4.5 上验证）
2. 把 `FikaInventoryResync.dll` 放进 **每个客户端**（含无头端）的 `BepInEx\plugins\`
3. 启动游戏，日志里会看到：

```
FikaInventoryResync v1.3.2 loaded. Enabled=True Cooldown=1s FixStuckLock=True
```

**⇒ 所有玩家都要装**（不只是主机）。主机会响应重发请求，客户端会发请求。

---

## 配置

配置文件：`BepInEx\config\com.local.fikainventoryresync.cfg`

### `[General]`

| 项 | 默认 | 说明 |
|---|---|---|
| `Enabled` | `true` | 总开关（三个补丁一起开关） |
| `RequestCooldownSeconds` | `1.0` | 同一个 netId 两次重发请求的最小间隔。防止严重失同步时刷屏请求 |
| `Verbose` | `true` | 记录每次**真的发出**的重发请求（被冷却抑制的只计数不记录） |

### `[InventoryLock]`

| 项 | 默认 | 说明 |
|---|---|---|
| `FixStuckInventoryLock` | **`true`** | ★ 检测残留锁（`Locked == true` 但玩家不在 Approach/DoorInteract/Stationary 状态）并清除。**关掉的话，一次被打断的交互就会让整个背包废到本局结束** |
| `CheckIntervalSeconds` | `1.0` | 残留锁检查频率 |

---

## 工作原理

### 补丁 ① — 探测失同步

挂在 Fika 的库存同步回调上，比对客户端与主机的物品状态。不一致时记下 netId。

### 补丁 ② — 请求重发

对失同步的 netId 发一次重发请求（受 `RequestCooldownSeconds` 节流）。

### 补丁 ③ — 残留锁看门狗

挂在插件的 `Update` 上（**不是** `UpdateLockedStatus` 的 Postfix —— 后者只在状态变化时调用，观察不到锁被遗留下来）。

每秒检查一次：

```
if (InventoryController.Locked && 玩家不在三个会上锁的状态里)
    ⇒ 判定为残留锁 ⇒ 清掉
```

日志只在**状态真的变化**时输出，不刷屏。

---

## 已知问题

- **不是万能的**：如果失同步的根因是主机侧数据本身错了，重发也没用
- **看门狗有 1 秒窗口**：极端情况下残留锁存在期间的那 1 秒内，物品操作仍会失败
- **只处理物品栏锁**：武器卡壳、动作状态机卡住属于另外的问题，本插件不管

---

## 构建

```powershell
dotnet build -c Release -p:EFT_DIR="你的 SPT 安装目录"
```

`EFT_DIR` 默认 `C:\SPT`，也是 `csproj` 里 `$(EFT_DIR)` 的默认值。产物在 `bin\Release\FikaInventoryResync.dll`。

需要 .NET SDK 9 或更新（项目用 `netstandard2.1` 目标框架，但 SDK 版本要够）。

---

## English

**Fika Inventory Resync** automatically repairs inventory desync in Fika co-op by requesting a resend from the host,
and includes a leftover-lock watchdog for the far more annoying failure mode.

Two distinct problems, both covered:

1. **Inventory desync** — your `InventoryController` diverges from the host's. Items you picked up aren't in your bag;
   items in your bag can't be used; other players see you holding a gun while you see empty hands.
   Fixable by asking the host to resend the inventory.

2. **Leftover inventory lock** — `InventoryController.Locked` is set by three movement states
   (`ApproachState` / `DoorInteractState` / `StationaryPlayerState`) and cleared in their `Exit`.
   If an interaction is interrupted, `Exit` never runs, the lock stays set, and
   `ItemController.CheckItemAction` throws `PlayerIsBusyError: Cannot apply {item} because {target} is currently being modified`
   for **every** item action until the raid ends. **Resending the inventory does not clear this** — it is a runtime flag,
   not inventory data.

Install on **every client including headless**, plus the host. Config at
`BepInEx\config\com.local.fikainventoryresync.cfg`. Build with
`dotnet build -c Release -p:EFT_DIR="<your SPT install>"`.

---

## 许可

MIT