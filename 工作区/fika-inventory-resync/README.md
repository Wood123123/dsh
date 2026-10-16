# Fika Inventory Resync

修复 **Fika 联机**里物品栏三类故障的 BepInEx 补丁插件。纯 Harmony 运行时补丁 ——
**不替换 Fika 任何文件、不改配置、删 DLL 即完全还原。**

> 适用 **SPT 4.1.6 + Fika 2.4.4 / 2.4.5 + Fika.Headless 1.5.3**
> **v1.3.2**

---

## 三类故障

| # | 问题 | 症状 | 修它的补丁 |
|---|---|---|---|
| **A** | 库存操作**在非发起方客户端上被应用两次** | `Not enough space in target`(122) · `item X is not located at <A>. It's at <B>`(120) · `Could not find item owner with id: X` | **Patch 3**（仅主机） |
| **B** | 切分堆叠时**新物品 id 在各端各自生成** | `Cloned item ID desync` · `Could not find item with id` · 卸载子弹时最常发生 | **Patch 4 + 1 + 2a + 2b** |
| **C** | **残留物品栏锁** | `Cannot apply {item} because {target} is currently being modified` · 装备手雷失败 · 手部卡在空手 | **LockFix 看门狗** |

**⇒ 三个是独立根因，必须一起修。只装其中一个不会好。**

---

## 根因 A：每一个库存操作在非发起方客户端上被重放两次

### 证据链（对着实机 `Fika.Core.dll` 反编译验证）

**1. 客户端发包时写 broadcast 字节，主机不写**

```
FikaClient::SendGenericPacket    IL 0x0012:  NetDataWriter.Put(System.Boolean)   ← 写
FikaServer::SendGenericPacket    IL:         没有 Put(Boolean)                    ← 不写
```

`ClientInventoryController.RunClientOperation` 传的是 `broadcast: true`。

**2. 主机因此转发两遍**

```csharp
// FikaServer.OnNetworkReceive
if (reader.GetByte() == 1)
    _netServer.SendToAll(...);                          // ← 转发 #1（原始字节）
switch (reader.GetEnum<EPacketType>()) { ... case Generic: HandleGenericPacket(...); }

//  → FikaServer.OnInventoryPacketReceived
if (result.Failed) { ...发 resync...; return; }
SendGenericPacket(in packet, ..., true, peer);          // ← 发送 #2（同一个描述符）
```

**3. 客户端不读 broadcast 字节**，所以两份包布局完全相同、各解析一次。

**⇒ 结论：每个库存操作，在每个非发起方客户端上都被重放两次。**

重复应用一个**已经应用过**的操作，产生的正是那些报错 —— 实测分布：`Not enough space in target`
在 netId 3 上重复 **79** 次、netId 2 上 **43** 次，完全无区分度的重复。

### 附带修好的第二个问题

主机**拒绝**操作时（`result.Failed`）会 `return`，**不做**发送 #2 —— 但转发 #1 **已经发出去了**。
于是其他客户端会应用一个主机明确拒绝的操作 → 两端镜像直接分叉。

**Patch 3 抑制转发 #1 之后**：拒绝的操作不再泄漏给其他客户端。

---

## 根因 B：卸载子弹时新分配的物品 id 在各端**必然不同**

### 机制

**1. EFT 有两个 `IDatabaseIdGenerator` 实现，行为不一致**

```csharp
// 真实执行路径 —— 每调用一次就前进一格
ItemController.get_NextId:
    ldfld _currentId ; call MongoID::op_Increment ; stfld _currentId
    ldloc.0        ← 返回自增【前】的值

// 描述符回放路径 —— 永远返回同一个常量，RollBack() 是空的
SplitOperationDescriptor/SplitIdGenerator.get_NextId:
    ldfld _nextId ; ret
```

**2. 克隆物品时按"物品 + 它的每一个内容物"反复取 id**

```
ItemManipulator::Split<T> → ItemExtensions::CloneItem<T>(T, IDatabaseIdGenerator)
CloneItemInternal: 对 StackSlot / Grid / Slot 里的每个内容物【递归】再取一次
```

**3. 是"合并"还是"切分"由**运行时的本地状态**决定**

```
ItemManipulator::ApplySingleItemToAddress:
   005E: TransferMax(...)   ← 目标堆叠还放得下 → 只改数量，不产生新 id
   0095: Split(...)         ← 放不下 → 切分，【产生新 id】
```

**4. 而 `MoveOperationDescriptor` 只带 `ItemId | From | To | DestroyedItems` —— 完全不携带新分配的 id。**

**⇒ 卸载子弹 = 把弹药堆叠从弹匣切分出来 = 分配新物品 id。Fika 只传"操作描述符"，
每一端各自用自己的计数器生成这个 id，因此必然不同。**

EFT 自带的检查会响：

```
[Error :Fika.Client] HandleInventoryPacket::Unable to process descriptor from netId 3,
  error: Cloned item ID desync. Expected ID: 6ac630c9147f66469b000059, real ID: 6ac630c9147f66469b000058
```

**Expected 与 real 只差 1、差 6 —— 就是两端计数器相差的步数。**

### Fika 尝试过同步计数器，但只在开局时一次

```
CoopGame.cs:268       ControllerId = fikaPlayer.InventoryController.CurrentId    ← 发起方上报
ObservedPlayer.cs:346 new ObservedInventoryController(player, profile, true, firstId, ...)
ObservedInventoryController.cs:32   _currentId = firstId;                        ← 镜像播种
```

**⇒ 只要任何一次应用失败 / 被重复投递 / 被空包 resync 清成 `default`，计数器就永久失步，
之后每一次切分都会在所有机器上产生不同 id。**

---

## 根因 C：残留物品栏锁

`InventoryController.Locked` 只被三个移动状态设置，在它们的 `Exit` 里清除：

```
ApproachState          （接近交互物）
DoorInteractState      （开门）
StationaryPlayerState  （原地蹲/趴）
```

**如果交互被打断（掉线、切图、死亡动画、主机卡顿），`Exit` 没被调用**，锁就一直挂着。
之后 `ItemController.CheckItemAction` 会抛：

```
PlayerIsBusyError: Cannot apply {item} because {target} is currently being modified
```

**⇒ 该玩家之后的一切物品操作全部失败，直到本局结束。**

**⚠ 这个锁不会被「重发库存」修掉** —— 重发换的是 inventory 数据，锁是 `InventoryController`
上的运行时标志，两回事。

---

## 六个补丁

| # | 目标 | 类型 | 装哪 | 作用 |
|---|---|---|---|---|
| **3** | `FikaServer.OnNetworkReceive` | **Transpiler** | **仅主机** | 把 broadcast 标志读取换成辅助函数：若是 `Generic`/`InventoryOperation` 就返回 0（不转发）。全方法**只有 1 个** `NetDataReader.GetByte()` 调用点，已静态核对；数量不符会**明确报错并保持惰性** |
| **4** | `EFT.SplitOperationDescriptor/SplitIdGenerator.get_NextId` | Prefix | **所有端** | 让回放用的 id 生成器像 `ItemController.get_NextId` 一样**前进**（返回当前值、存回自增后的值），而不是永远返回常量 |
| **1** | `ObservedInventoryController.CreateOperationFromDescriptor` | Postfix | 客户端 | 重放失败时发一个空 `ResyncInventoryIdPacket(netId)` 求助。带 per-netId 冷却（默认 1s），被抑制的请求只计数不记录 |
| **2a** | `FikaServer.OnResyncInventoryIdPacketReceived` | Prefix | 主机 | `MongoId` 为空则**跳过**。空包语义是"请求"而非"赋值"，无条件 `SetNewID` 会把镜像 `_currentId` 清成 `default` |
| **2b** | `FikaClient.OnResyncInventoryIdPacketReceived` | Prefix | 客户端 | 同上，但目标是**自己的玩家**时放行（否则求助没人应答） |
| **6** | `ObservedFirearmController.IEventsConsumerOnShellEject` | Prefix | 所有端 | `_weaponManager` 为 null 时跳过（见下） |
| — | **LockFix 看门狗**（不是 Harmony 补丁） | 插件 `Update` | 客户端 | 每秒检查残留锁并清除 |

### ★ Patch 5 已被**刻意移除**（v1.3.1）

它挂在 `ObservedFirearmController.Start()` 上，用一个 **Finalizer 吞掉** `NullReferenceException`。

**为什么移除：**
```
① Finalizer 返回 null = 把异常吞了，Unity 的生命周期再也看不到
② 但对象的 stfld _weaponManager 在抛异常那句【之后】
   ⇒ 吞掉异常 = 对象保持半初始化状态
③ 无头端在 "Headless client is ready" 之后立刻硬崩
   —— 正是第一批 observed firearm controller 初始化的时候
   ⇒ 没有托管异常、没有 dump
```

**Patch 6 保留** —— 它只跳过 `_weaponManager` **可证明为 null** 的方法，不碰别的。

---

## 安装

```
<EFT>\BepInEx\plugins\FikaInventoryResync\FikaInventoryResync.dll
```

| 补丁 | 需要的端 |
|---|---|
| Patch 3 | **仅主机（无头端）** —— 主机是唯一执行转发的地方 |
| Patch 4 | **所有端**（主机 + 所有客户端）—— 修的是每一端各自的回放路径 |
| Patch 1 / 2a / 2b / 6 / LockFix | 装了的端才受益 |

**⚠ 同一 GUID 只能存在一份。** 如果 `plugins\` 根目录下也有一份 `FikaInventoryResync.dll`，删掉，只留子文件夹里那份。

---

## 配置

`<EFT>\BepInEx\config\com.local.fikainventoryresync.cfg`

### `[General]`

| 项 | 默认 | 说明 |
|---|---|---|
| `Enabled` | `true` | 总开关（**所有**补丁 + 看门狗一起开关） |
| `RequestCooldownSeconds` | `1.0` | 同一 netId 两次求助的最小间隔。防止严重失步时刷屏请求 |
| `Verbose` | `true` | 记录每次**真的发出**的求助（被抑制的只计数不记录） |

### `[InventoryLock]`

| 项 | 默认 | 说明 |
|---|---|---|
| `FixStuckInventoryLock` | **`true`** | ★ 残留锁看门狗。**关掉的话，一次被打断的交互就会让整个背包废到本局结束** |
| `CheckIntervalSeconds` | `1.0` | 检查频率（内部下限 0.2s） |

---

## 怎么确认生效

启动日志搜 `FikaInventoryResync`，应该看到 **6 行 OK**：

```
[Info :FikaInventoryResync] FikaInventoryResync v1.3.2 loaded. Enabled=True Cooldown=1 FixStuckLock=True
[Info :FikaInventoryResync] Patch1  OK: client resync request hooked on ...ObservedInventoryController::...CreateOperationFromDescriptor
[Info :FikaInventoryResync] Patch2a OK: host resync guard on FikaServer::OnResyncInventoryIdPacketReceived
[Info :FikaInventoryResync] Patch2b OK: client resync guard on FikaClient::OnResyncInventoryIdPacketReceived
[Info :FikaInventoryResync] Patch3  OK: host inventory-operation de-duplication transpiler on FikaServer::OnNetworkReceive
[Info :FikaInventoryResync] Patch3: replaced the broadcast-flag read (1 call site, as expected).
[Info :FikaInventoryResync] Patch4  OK: split replay id generator now advances (was returning a constant)
[Info :FikaInventoryResync] Patch6  OK: shell-eject is skipped when _weaponManager is null ...
```

**判定标准：**
```
✅ 客户端 HandleInventoryPacket::Unable to process descriptor  应从三百多条降到接近 0
✅ Cloned item ID desync  应完全消失
✅ IEventsConsumerOnShellEject 的 NullReferenceException 刷屏 应停止
✅ 「装备失败 / 手部卡住」应能自动恢复（日志里会有 [LockFix] 行）
```

**⚠ 如果任何 `Patch*` 报 `SKIPPED` 或 `expected exactly 1 ... but found N`，补丁会明说且保持惰性
（不会误改语义）—— 把那一行发出来。**

---

## 构建

```powershell
dotnet build -c Release -p:EFT_DIR="你的 SPT 安装目录"
```

`EFT_DIR` 默认 `C:\SPT`，也是 csproj 里 `$(EFT_DIR)` 的默认值；指错会立刻报一条清楚的错误。

**本地客户端与无头端的 `Fika.Core.dll` 已验证字节相同（2.4.4.0），一份构建两边通用。**

---

## 已知限制

- **根因 B 只能"对齐"，不能根治。** 就算各端计数器完全同步，Fika 用的仍是
  "广播操作描述符、各端各自重放"这套模型 —— 它要求各端物品树逐字节一致。
  只要有一个 mod 改了物品树、或一次应用被丢弃，就会再次失步。
  **真正根治需要改协议层**（让每个操作携带它新分配的 id）。
  Patch 1/2 的作用是让失步能**被拉回来**，而不是让它不发生。
- **Transpiler 依赖 `OnNetworkReceive` 的结构。** Fika 升级后若该方法重写，
  Patch 3 会**明确报错并保持惰性**（不会误改语义），届时需重新核对。
- **看门狗有 1 秒窗口** —— 极端情况下残留锁存在期间那 1 秒内，物品操作仍会失败。
- **只处理物品栏锁。** 武器卡壳、动作状态机卡住属于另外的问题。
- **主机玩家自身没有"求助"路径。**
- **与 `FikaIdResync` 功能重叠**，建议只留一个。

---

## English (brief)

BepInEx patch plugin fixing **three independent inventory failures** in Fika co-op.
Pure Harmony runtime patches; delete the DLL to fully revert.

| # | Problem | Fixed by |
|---|---|---|
| **A** | Every inventory operation is **replayed twice** on non-owner clients | **Patch 3** (host-only transpiler) |
| **B** | Split operations allocate **divergent item ids** per client | **Patch 4 + 1 + 2a + 2b** |
| **C** | **Leftover inventory lock** blocks all item actions until raid end | **LockFix watchdog** |

Install to `<EFT>\BepInEx\plugins\FikaInventoryResync\`. **Patch 3 is host-only; Patch 4 must be on every end.**
Config: `BepInEx\config\com.local.fikainventoryresync.cfg`.
Build: `dotnet build -c Release -p:EFT_DIR="<SPT install>"`.

---

## 许可

MIT