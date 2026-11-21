# Fika 联机同步 · 问题清单与防护体系

> 适用范围：SPT 4.1.6 / EFT 0.16.9.40743 / BepInEx 5.4.23.5 / Fika.Core 2.4.6（源码级补丁）/ Fika.Headless 1.5.3 / FikaServer 2.4.2
> 五端部署：客户端 `F:\EFT`、服务端 `EFT-SERVER`、无头 `EFT-headless` / `EFT-headless1` / `EFT-headless2`
> 本文档记录：**遇到过哪些联机同步问题 → 根因（含代码级证据）→ 我们做了什么 → 文件在哪 → 怎么验证 → 还有什么风险**

---

## 0. 一页速查

| 编号 | 现象 | 根因（一句话） | 我们的防护 | 状态 |
|---|---|---|---|---|
| **S1** | 背包乱、捡不起东西、手卡空、`Not enough space in target` | 库存操作被主机**投递两遍** + 切分物品的 id **各端各自生成** + 残留 `ItemController.Locked` | `FikaInventoryResync` v1.3.2 + `Fika.Core` 源码补丁 | ✅ 已部署 |
| **S2** | 重连后位置回到原地、或重连进来**没有撤离点** | 原版只遍历 `HumanPlayers`，人不在列表里就不回 `OwnCharacter`；`ExfiltrationReceived` 开局即为 true ⇒ `InitExfils` 空转 | `DshReconnectResync` 1.2.0 | ✅ 已部署 |
| **S3** | 中途加入瞬间刷 `no player with id N for packet type HealthSync` + `OnNetworkReceive` NRE | 加入者还没收到权威局内状态，却已经开始收广播包 | `LateJoin.Client/Server` 0.1.0 | ✅ 已部署 |
| **S4** | 无头开图崩：`transition a task to a final state when it had already completed` | MapVariants 在 `LocalGameMatching` 这个 async 方法里同步改图 | `DshMapVariantsHeadless`（presync + 屏蔽） | ⏸ 随 MapVariants 停用而 `.off` |
| **S5** | 进战局没有撤离点（转移局/地图对不上） | 客户端用过期地图建本地战局，服务端按错地图生成 loot | `FikaLocationFix` | 🚫 已写好未部署 |
| **S6** | 开 Terminal 时主机每帧卡 57 ms | 自定义图同步器每帧全量重算 crane/zones | 优化版 `ManimalTerminalFika` | ↩ 已回退原版 |
| **S7** | 无头报 `Requested value 'ruafRiflemanSenior' was not found` 等 | 无头**不参与** NarcoNet 分发，缺 patcher/mod | 五端同装铁律 + 手工补件 | ✅ 已修 |
| **S8** | 客户端反复重下文件 / 启动报 `same key has already been added` / 服务端 `No Assemblies found in path` | NarcoNet 的哈希清单、syncPaths 撤销、`.dll.off` 与 SPT `ModLoader` 冲突 | 清单同步脚本 + 停用规范 | ✅ 已规范 |
| **S9** | 存档 375 件被**删除** | 大依赖换到一半就启动服务端，`ModValidator` 拒绝加载全部 mod ⇒ 模板查不到的物品被清理 | 换依赖流程铁律 + 备份 | ⚠ 已恢复、已立规矩 |

---

## 1. 环境基线与部署矩阵

### 1.1 版本配对（勿单独升级）

| 组件 | 版本 | 说明 |
|---|---|---|
| SPT | 4.1.6 | 服务端 `SPT.Server.exe` |
| EFT | 0.16.9.40743 | |
| BepInEx | 5.4.23.5 | + `spt-prepatch` 4.1.6 + `UnityToolkit-Prepatcher` |
| Fika.Core | **2.4.6**（打了 4 项源码补丁） | 1,973,248 B，sha16 `12C3B60831D1EC60` |
| Fika.Headless | **1.5.3** | sha16 `EB94B5DF024A` |
| FikaServer（服务端 mod） | **2.4.2** | 官方配对：Core 2.4.6 ↔ Server 2.4.2 |

> Fika 大版本升级后，**所有 Harmony 补丁都要复查**（我们的插件依赖 `FikaServer._coopHandler`、`FikaClient.OnNetworkReceive` 等内部签名）。

### 1.2 五端与端口

| 端点 | 路径 | UDP 端口 | 档案 ID |
|---|---|---|---|
| 客户端 | `F:\EFT` | — | Wood `6aa79ee646960049c4857451` |
| 服务端 | `Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-SERVER` | 6969 | — |
| 无头 0 | `...\EFT-headless` | 25565 | `6a97214f53af47255441a827` |
| 无头 1 | `...\EFT-headless1` | 25567 | `6acb08501f444127e41896e9` |
| 无头 2 | `...\EFT-headless2` | 25566 | `6aa00886913b616520870e76` |

> 端口冲突的真实症状：两台无头都用 25565 时，`SocketException: 每个套接字地址只允许使用一次`，随后**无头自杀**。

### 1.3 同步相关 DLL 现状（2026-11-21 实测哈希）

```
Fika.Core.dll            12C3B60831D1EC60   五端一致
FikaInventoryResync.dll  2C70EEA745BDB488   五端一致
DshReconnectResync.dll   D0272A95C0FE8313   五端一致
LateJoin.Client.dll      D40F7E4B8559B7DD   五端一致
DshMapVariantsHeadless   11776 B *.dll.off   仅三台无头（随 MapVariants 停用）
```

---

## 2. S1 库存失同步（最重的一类）

这是所有"背包乱了/东西捡不起来/手卡住"的总根因，由**三个互相独立的 bug**叠加而成，必须一起修。

### 2.1 根因 A：每个库存操作在非发起方客户端上被重放两次

**代码证据**（对着实机 `Fika.Core.dll` 反编译）：

```
FikaClient::SendGenericPacket   IL 0x0012:  NetDataWriter.Put(System.Boolean)   ← 客户端写 broadcast 字节
FikaServer::SendGenericPacket   IL:         没有 Put(Boolean)                    ← 主机不写
```

`ClientInventoryController.RunClientOperation` 传的是 `broadcast: true`。主机因此**发两遍**：

```csharp
// FikaServer.OnNetworkReceive
if (reader.GetByte() == 1)
    _netServer.SendToAll(...);                    // 转发 #1：原始裸字节

//  → FikaServer.OnInventoryPacketReceived
if (result.Failed) { ...发 resync...; return; }
SendGenericPacket(in packet, ..., true, peer);    // 发送 #2：同一个描述符
```

客户端不读 broadcast 字节，两份包布局相同，于是**各解析一次 ⇒ 重放两次**。重复应用的结果就是：

```
Not enough space in target (122)
item X is not located at <A>. It's at <B> (120)
Could not find item owner with id: X
```

实测分布：`Not enough space in target` 在 netId 3 上 79 次、netId 2 上 43 次——无区分度的重复。

**附加伤害**：主机**拒绝**操作时走 `return`（不发 #2），但转发 #1 已经发出 ⇒ 其他客户端会应用一个被主机明确拒绝的操作 ⇒ 两端镜像直接分叉。

### 2.2 根因 B：切分堆叠时，新物品 id 在各端"必然不同"

```
真实执行路径 ItemController.get_NextId:
    ldfld _currentId ; call MongoID::op_Increment ; stfld _currentId
    ldloc.0        ← 返回自增【前】的值（每调一次前进一格）

描述符回放路径 SplitOperationDescriptor/SplitIdGenerator.get_NextId:
    ldfld _nextId ; ret    ← 永远返回同一个常量，RollBack() 为空
```

而 `MoveOperationDescriptor` 只携带 `ItemId | From | To | DestroyedItems`——**不带新分配的 id**。所以：

> 卸载子弹 = 把弹药堆叠从弹匣切分出来 = 需要新物品 id。Fika 只传描述符，各端用自己的计数器生成 ⇒ 必然不同。

EFT 自带检查会立刻报：

```
[Error :Fika.Client] HandleInventoryPacket::Unable to process descriptor from netId 3,
  error: Cloned item ID desync. Expected ID: 6ac630c9147f66469b000059, real ID: 6ac630c9147f66469b000058
```

差 1、差 6 —— 就是两端计数器相差的步数。

Fika 只在**开局时同步一次**计数器（`CoopGame.cs:268 ControllerId = ...CurrentId` → `ObservedPlayer.cs:346` → `ObservedInventoryController.cs:32 _currentId = firstId`）。只要有一次应用失败/重复投递/被空包 resync 清成 `default`，计数器**永久失步**。

### 2.3 根因 C：残留物品栏锁

`InventoryController.Locked` 只被三个移动状态设置、在它们的 `Exit` 里清除：

```
ApproachState        （接近交互物）
DoorInteractState    （开门）
StationaryPlayerState（原地蹲/趴，另有 Spawned 写入点）
```

交互被打断（掉线、切图、死亡动画、主机卡顿）⇒ `Exit` 没被调用 ⇒ 锁永久挂着 ⇒ 之后

```
PlayerIsBusyError: Cannot apply {item} because {target} is currently being modified
```

该玩家**本局一切物品操作全部失败**。注意：**重发库存修不掉它**（锁是 `InventoryController` 的运行时标志，不是 inventory 数据）。

---

### 2.4 防护一：`FikaInventoryResync` v1.3.2（Harmony 运行时补丁）

位置：`工作区\fika-inventory-resync\`
产物：`FikaInventoryResync.dll`，sha16 `2C70EEA745BDB488`，部署五端 `BepInEx\plugins\FikaInventoryResync\`
配置：`BepInEx\config\com.local.fikainventoryresync.cfg`

| # | 目标 | 类型 | 装哪 | 作用 |
|---|---|---|---|---|
| 3 | `FikaServer.OnNetworkReceive` | Transpiler | **仅主机** | 把 broadcast 标志读取换成辅助函数：`Generic`/`InventoryOperation` 返回 0（不转发）。全方法只有 1 个 `GetByte()` 调用点，已静态核对；数量不符会报错并**保持惰性** |
| 4 | `EFT.SplitOperationDescriptor/SplitIdGenerator.get_NextId` | Prefix | **所有端** | 让回放用的 id 生成器**前进**（返回当前值、存回自增后的值），不再永远返回常量 |
| 1 | `ObservedInventoryController.CreateOperationFromDescriptor` | Postfix | 客户端 | 重放失败时发空 `ResyncInventoryIdPacket(netId)` 求助，per-netId 冷却默认 1s |
| 2a | `FikaServer.OnResyncInventoryIdPacketReceived` | Prefix | 主机 | `MongoId` 为空则**跳过**（空包语义是"请求"不是"赋值"；无条件 `SetNewID` 会把镜像 `_currentId` 清成 `default`） |
| 2b | `FikaClient.OnResyncInventoryIdPacketReceived` | Prefix | 客户端 | 同上，但目标是**自己**时放行 |
| 6 | `ObservedFirearmController.IEventsConsumerOnShellEject` | Prefix | 所有端 | `_weaponManager` 可证明为 null 时跳过 |
| — | **LockFix 看门狗**（非 Harmony） | 插件 `Update` | 客户端 | 每秒清残留锁；`CheckIntervalSeconds` 默认 1.0（内部下限 0.2） |

**刻意的取舍：Patch 5 已移除（v1.3.1）**
它用 Finalizer 吞掉 `ObservedFirearmController.Start()` 的 NRE。后果：
```
① Finalizer 返回 null = 异常被吞，Unity 生命周期看不到
② 但 stfld _weaponManager 在抛异常那句【之后】
   ⇒ 对象保持半初始化
③ 无头端在 "Headless client is ready" 之后立刻硬崩（无托管异常、无 dump）
```
Patch 6 保留——它只跳过可证明为 null 的分支。

**日志（启动应看到 6 行 OK）**
```
[Info :FikaInventoryResync] FikaInventoryResync v1.3.2 loaded. Enabled=True Cooldown=1 FixStuckLock=True
```

---

### 2.5 防护二：`Fika.Core` 源码级补丁（4 项）

位置：`工作区\fika-2.4.6-patched\`（基线 `project-fika/Fika-Plugin` tag **v2.4.6**，`9572504 Fix nullref on death`）
产物：`Fika.Core.dll` 1,973,248 B，五端一致

#### ① 网络层：库存操作不再投递两遍（根因）

`Fika.Core\Networking\FikaServer.cs`：

```csharp
if (reader.GetByte() == 1)
{
    if (!IsInventoryOperationPacket(reader))          // ★ 新增：peek 不消费
        _netServer.SendToAll(reader.GetRemainingBytesSpan(), deliveryMethod, peer);
}
```

新增 `IsInventoryOperationPacket(NetPacketReader)`：存 `Position` → 读 `EPacketType`/`EGenericPacketType` → `finally` 恢复 `Position`；任何失败路径返回 `false`（退回原行为）。
等价于上游 3.0.1 的 `if (subType == EGenericPacketType.InventoryOperation) relay = false;`，但是**源码级**，不依赖 IL 结构。

#### ② 逃生舱 `NativeInteractionRelease`（新文件）

`Fika.Core\Main\Utils\NativeInteractionRelease.cs`

拾取走 `EFT.PickUpState`（`EPlayerState.Pickup`）+ 动画交互队列。当驱动它的库存操作始终没有回报终态时，状态机**永久停在 PickUpState**：视角不能转、不能移动、`CanInteract` 一直返回 `"Can't interact in Pickup"` ⇒ 地面战利品再也捡不起来（但开枪、背包还能用——所以看起来"人能走"却"捡不起东西"）。

`Release(player, reason)` **每步独立 try/catch**，只恢复表现层状态，绝不触碰物品/地址/操作：

```
AnimatedInteractions.ForceStopInteractions()
MovementContext.StopAnyInteractions()
MovementState.Cancel()
BaseMovementState.Exit(false)
MovementContext.ProcessStateEnter(States[EPlayerState.Idle])
MovementContext.SetInteractInHands(EInteraction.None)
MovementContext.PlayerAnimatorSetInteraction(false, false)   ← 两个参数
MovementContext.ToggleBlockInputPlayerRotation(false)
Player.InteractableObject = null
Player.ForceInteractionsChanged()
```

`IsStuck()` 判定：`CurrentState ∈ {Pickup, Loot, DoorInteraction, BreachDoor, Plant}` **或** `CanInteract != null` **或** `AnimatedInteractions` 卡住 ⇒ 三者都不成立就什么都不做（避免打断合法换弹/开门/检视/投掷）。

#### ③ 看门狗 `PickupStuckWatchdog`（新文件）

`Fika.Core\Main\Utils\PickupStuckWatchdog.cs`，挂在 `CoopHandler.Update()`（host/client 都有）。

**为什么不能只在"操作失败"时释放**：状态可能比一个*报告成功*的操作活得更久 ⇒ 必须观察**超时**而不是观察失败。

| 项 | 值 |
|---|---|
| 检查间隔 | 1 s |
| Loot / Pickup 超时 | 5 s |
| DoorInteraction / BreachDoor | 8 s |
| Plant | 20 s |
| 修复冷却 | 8 s |

开销：入口每帧，但只读几个属性、正常时立即 `return`、不分配 ⇒ 亚微秒级。

#### ④ 残留物品栏锁清理

同一看门狗内 `TryClearStuckInventoryLock()`：判据是 `ItemController.Locked == true` **且** 当前状态名不在那三个写入态里。

```
⚠ MovementContext.StateLocksInventory 是只写属性（没有 getter），读不了 ⇒ 只能用状态名判定
⚠ 状态为 null（加载/卸载中）时不动，那可能是引擎的正常中间态
```

#### 编译注意

```
cd Fika.Core
dotnet build -c Release
```

`References\hollowed.dll` **必须是 upstream 的**（9,271,296 B）。用本地 `Assembly-CSharp.dll`（16,233,472 B）会报 **90 个错**（private 字段不可访问）。其余 28 个 dll 用本地的（`F:\EFT\EscapeFromTarkov_Data\Managed` + `F:\EFT\BepInEx\plugins\spt`）。

---

### 2.6 为什么两套都留着

| 层 | 解决什么 | 稳定性 |
|---|---|---|
| `Fika.Core` 源码补丁 | 网络层根因（双投递）+ 表现层逃生舱/看门狗 | 稳，但升级 Fika 要重新合并 |
| `FikaInventoryResync` | id 分叉（Patch 4）、空包语义（2a/2b）、残留锁（LockFix） | 与 Fika 版本解耦的运行时兜底 |

Patch 3 与源码补丁①解决同一问题，属于**双保险**；当 Fika 升级后源码补丁未跟上时，Harmony 版仍可顶住。

---

## 3. S2 重连：位置丢失与撤离点消失

### 3.1 现象

- 重连回来**角色停在原地**（位置/朝向没恢复）
- 重连进来**一个撤离点都没有**（只剩静态转移点）

### 3.2 根因

**位置**：`FikaServer.OnReconnectPacketReceived`

```csharp
if (packet.InitialRequest)
{
    foreach (FikaPlayer humanPlayer in _coopHandler.HumanPlayers)
    {
        if (humanPlayer.ProfileId == packet.ProfileId && humanPlayer is ObservedPlayer)
        {
            // 才回 OwnCharacter（Profile + 位置 + 朝向 + 血量）
        }
    }
    return;   // ← 找不到就直接返回
}
```

一旦重连者已被移出 `HumanPlayers`，客户端永远收不到 `OwnCharacter`。

**撤离点**：`ClientGameController` 里 `InitExfils()` 的循环条件是 `while (!ExfiltrationReceived)`，而 `ExfiltrationReceived` 在开局就是 `true`（且重连包类型里**没有** Exfiltration 这一项）⇒ 重连后不补拉 = 没有撤离点。

### 3.3 做法：`DshReconnectResync` 1.2.0

位置：`工作区\dsh-reconnect-resync\ReconnectResyncPlugin.cs`
产物：`DshReconnectResync.dll`，sha16 `D0272A95C0FE8313`，五端 `BepInEx\plugins\DshReconnectResync\`

1. **重连兜底（主机侧）**：prefix 记录原版能否命中；postfix 在未命中时从 `GameWorld.AllAlivePlayersList` / `AllPlayersEverExisted` 兜底找人，补发一份 `OwnCharacter`。
2. **撤离点补拉**：postfix `OnReconnectPacketReceived` 只置 `_pendingExfilRefresh`；`Update()` 里起协程，最多 15 次 × 1s 重试：
   - 每次重新解析 `Singleton<IFikaGame>.Instance.GameController`（避免拿到旧实例）
   - `ExfiltrationReceived = false`
   - 发 `new RequestPacket { Type = ERequestSubPacketType.Exfiltration }`（`SendData(ref req, DeliveryMethod.ReliableOrdered)`）
   - 主机侧把 `ExfiltrationRequest` 回包按名字套用，日志 `Received {n} exfils from the server`
3. **End 键手动重同步**：遍历 `CoopHandler.Players` 逐个 `Snapshotter.Clear()`；主机再向每个 peer 广播 `ClearSnapshotterPacket`；1 秒防连按。

配置：`[Reconnect] ExfilRefresh = true`

**已知副作用**：客户端处理 `ClearSnapshotterPacket` 时会顺手 `HealthBar.RemoveAllActiveEffects()` ⇒ **buff 图标被清空**（数据没丢，只是 UI）。

**日志关键字**
```
[Resync] patched FikaServer.OnReconnectPacketReceived (host side)
[Resync] initial reconnect request from <profileId> | vanilla-can-handle=True|False | humans=N
[Resync] 已补发 OwnCharacter -> <profileId> pos=(x, y, z) health=ok
[Resync] manual resync: localCleared=N players=M peers=K
```

> 设计要点：`ClearSnapshotterPacket` 的类型名**不在** `ilspycmd -l c` 的类清单里，必须反射按名解析（真实全名 `Fika.Core.Networking.Packets.Communication.ClearSnapshotterPacket`）。

---

## 4. S3 中途加入（LateJoin）

### 4.1 现象

`Player.log` 里在加入瞬间刷屏：

```
no player with id 2 for packet type HealthSync      （807 次）
NullReferenceException at Fika.Core.Networking.FikaClient.OnNetworkReceive   （936 次）
```

直到 `[LateJoin] Received authoritative active-raid state from the Fika host.` 才收敛。

### 4.2 结论

这是**时序边界**问题：加入者还没拿到权威局内状态，就已经开始收广播包。稳态局不受影响 —— 出问题的永远是"边界"：**中途加入 / 重连 / 库存镜像 / 无头 + 自定义图**。

### 4.3 产物

`Toliann/LateJoin` 源码编译：
- `LateJoin.Client.dll` 16,896 B `D40F7E4B8559` → 五端 `BepInEx\plugins\LateJoin\`
- `LateJoin.Server.dll` 10,240 B `8A13084D072C` → `SPT_Runtime\user\mods\LateJoin\`

> 该版本 `0.1.0/0.2.0` **没有** `Config.Bind`（网上流传的 6 键配置指南属于更高版本，不适用）。

---

## 5. S4 无头开局崩溃：MapVariants 的 raid-start sync

### 5.1 现象

```
[Error :Fika.Headless] Exception caught during raid init:
  An attempt was made to transition a task to a final state when it had already completed.
  at AsyncTaskMethodBuilder.SetResult
  at EFT.TarkovApplication+CG_LocalGameMatching.MoveNext
  at Fika.Headless.FikaHeadlessPlugin.BeginFikaStartRaid
```

### 5.2 根因

MapVariants 在 `EFT.TarkovApplication.LocalGameMatching`（**async 方法**）上挂 Prefix（`MapVariants.Client.RaidStartSync.Before`），里面做三件事：

1. `VariantSync.Ask()` —— 同步 HTTP 问服务端 `/mapvariants/hostvariant?locationId=`
2. `BackportSuspend.SetChoice()`
3. `BackportSuspend.Apply(..., "the raid start")` —— 当场换图

进入这条路的前提是 `RaidStartPolicy.NeedsServerAnswer`：`if (string.IsNullOrEmpty(sessionChoice)) return true;`
无头没有地图选择界面 ⇒ 会话里永远没 choice ⇒ **每局都走**。客户端有界面会先选，所以只有无头崩。

### 5.3 做法：`DshMapVariantsHeadless`

位置：`工作区\dsh-mapvariants-headless\`
- 在 `LocalGameMatching` 上挂自己的 Prefix（`Priority.First`，先跑）
- 读 `TarkovApplication._raidSettings.LocationId`
- 自己走 `VariantSync.Ask → SetChoice → Apply`
- 屏蔽 `RaidStartSync.Before`（`return false`），让它那段逻辑不再进入 async 方法

配置 `com.dsh.mapvariantsheadless.cfg`：`Enabled` / `Mode=presync|skip` / `ApplyVariant` / `Verbose`

**踩过的坑（重要）**：类型名必须是 `MapVariants.Client.Api.Maps`。写成 `MapVariants.Client.Maps` 时 `AccessTools.TypeByName` 返回 null ⇒ 闸门静默失效 ⇒ 照样崩。见经验卡 `procedures\补丁-TypeByName静默失败.md`。

**状态**：MapVariants 停用后，三台无头的 `DshMapVariantsHeadless.dll` 已改名 `.dll.off`；恢复 MapVariants 时同步改回。

---

## 6. S5 转移局/地图不匹配导致"没有撤离点"（未部署）

### 6.1 根因

客户端带着**过期地图**创建本地战局时，服务端按**错地图**生成 loot；客户端再用这份 loot 初始撤离点，而场景由主机决定 ⇒ 出口名对不上 ⇒ 一个撤离点都没有。

证据：战局码 `Y0EZ6N` 主机 `EFT-headless1 location=Sandbox`，客户端 `Initializing Exfils: Id bigmap, Exits: 11`。

### 6.2 做法：`FikaLocationFix`（服务端补丁）

位置：`工作区\fika-location-fix\`
原理：在 `LocationLifecycleService.StartLocalRaidAsync` 之前，用 Fika 战局的真实地图覆盖请求里的 location。
- 转移请求带 `Transition.TransitionRaidId` ⇒ 直接跳过
- FikaServer 自己会在 `StartLocalRaidAsync` 里用请求 location 覆盖，所以我们用 `[HarmonyPriority(Priority.First)]` 先跑
- 只在"请求图 ≠ 战局图"时改；相等时零副作用

**状态**：已编译（7,680 B `71D5FC755AF5`），**未部署**。部署后验证点：转移局**绝不能**出现 `corrected` 行；正常局不该出现；转移后重连应出现 `start location corrected: request='bigmap' -> match='TarkovStreets'`。

---

## 7. S6 自定义图 Terminal 的 Fika 同步器性能

### 7.1 现象

`SyncDriver.Update` 337 帧共 19,307 ms（≈57 ms/帧，峰值 484 ms）——主机几乎卡死。

### 7.2 做法

优化 `ManimalTerminalFika`：缓存 crane root/zones、扫描周期 0.5 s、每帧最多处理 32 条 pending ⇒ 35,840 B `50D9ACE826F94E1D`。

### 7.3 结局（诚实记录）

尝试在主机侧加 `TerminalSync.HeadlessHost` / `AcceptPeerActor` 让无头也参与装备柜分配 ⇒ **无头开 Terminal 直接崩**（还是 `transition a task to a final state`）⇒ 已回退，`ManimalTerminalFika.dll` 用测试服原版（`50D9ACE826F94E1D`）。
现状：**客户端当主机可以正常开 Terminal；无头当主机仍崩**，该崩溃自 11-17 起未解。

---

## 8. S7 无头缺件导致的"同步之后才炸"

### 8.1 为什么会出现

**无头不跑 NarcoNet 客户端**（`MadManBeavis-NarcoNet` 不存在），所有插件/mod 只能手动拷。于是出现"客户端 82 个插件目录 / 无头 67 个"这种缺口。

### 8.2 典型症状

```
Requested value 'ruafRiflemanSenior' was not found
  ← BossLocationSpawn.ParseBossName ← BossSpawnScenario.Init
```

真因不是枚举没下发，而是**正式服缺 `BepInEx\patchers\RUAFComeHome\RUAFComeHome.dll`**（7,680 B `2C7F0081D1A40ABC`，测试服有）。

### 8.3 我们的失误与纠正

我曾自写 `dsh-custom-spawn-types`（预补丁往 `EFT.WildSpawnType` 加值），把 scourge/blackDiv 也塞进去 ⇒ `Manimal.Scourge.Prepatch` 抛 `EFT already defines that enum name` ⇒ **BepInEx 整个 Assembly-CSharp 补丁中止**（连带 `SptPrePatcher` 全没跑）。最终只保留 RUAF 七项、数值对齐服务端，后来按要求从五端删除。

### 8.4 铁律

1. 客户端装的东西，**三台无头也要装**（客户端 141 dll vs 无头 107 的那种差距就是灾难）
2. `patchers` 与 `plugins` **分开过一遍**
3. 装完必须**验证无头能开局**
4. 无头不要用 junction 指向别的实例（会踩 `file is being used by another process`）

见经验卡 `procedures\诊断-无头缺插件导致的报错.md`。

---

## 9. S8 NarcoNet 分发层（同步的"下层"）

### 9.1 机制速记

- 服务端 `NarcoNet-Server\config.yaml` 的 `syncPaths` 决定"分发什么"，`exclusions` 决定"不分发什么"
- `data\NarcoNet_KnownFiles.txt` / `NarcoNet_KnownFiles_headless.txt` 是**哈希清单**
- 客户端 `NarcoNet_Data\LocalHashes.json` 是它**上次同步到的基线**
- **`Delete Removed Files` 默认 ON**：源端删了的文件，客户端会跟着删

### 9.2 三个真实事故

**(a) 改了文件不更新清单哈希 ⇒ 客户端反复下载**
换完服务端某个 dll 后，必须同步 `KnownFiles` 里那行的 SHA256，否则客户端每次都认为"版本不对"。

**(b) 撤销 `syncPaths` 的一条 ⇒ 客户端启动报重复键**
```
Could not load MadManBeavis's NarcoNet due to error analyzing mod files:
An item with the same key has already been added. Key: <路径>
```
原因：客户端缓存的是"上次同步到的路径清单"，服务端撤销后两边清单合并 ⇒ 同一个 key 进两次 ⇒ 直接加载失败。
修法：在客户端 `BepInEx\config\com.madmanbeavis.narconet.cfg` 里删掉该路径的整块（4 行，注意别剩孤立的 `# Default value:`）。

**(c) 服务端用 `.dll.off` 停用 mod ⇒ SPT 直接报错**
```
Exception occured while loading a mod at path: ./user/mods/<mod>
No Assemblies found in path
```
BepInEx 会忽略 `.dll.off`，但 **SPT 的 `ModLoader` 不会**——目录里有东西却没 dll 就抛异常。

**⇒ 停用规范（已固定）**
| 位置 | 停用方式 |
|---|---|
| 客户端 / 分发源 `BepInEx\plugins` | 目录内 `*.dll` → `*.dll.off`（原地保留，NarcoNet 只搬小文件） |
| **服务端** `SPT_Runtime\user\mods` | dll **保持原名**，整目录移到 `user\mods_disabled\`（服务端 mod 不参与分发，移出不会让客户端重下） |

### 9.3 我们做过的优化

- **哈希瘦身**：`BepInEx/plugins` 全量 12.6 GB，原本每次同步都 SHA256 → 28.6 s；加 28 条排除后降到 1–2 s
- **大包排除**（只停校验/下发，**不删客户端已有文件**）：`VisitAPI/rooms` 4.14 GB、`ManimalTerminal` 4.08 GB、`WTT-ContentBackport` 9.3 GB、Icebreaker 1.77 GB、Interchange 1.49 GB、`RaidReview` 1.10 GB
- **反例教材**：`BepInEx/config/VisitAPI` 加进 `syncPaths` 整目录校验——客户端里一个多余的假 `.dlg` 会**覆盖**服务器的对话树，导致"点拜访没反应"（详见 9.4）

### 9.4 一个值得记住的案例：假 `.dlg` 覆盖

VisitAPI 以 `BepInEx\config\VisitAPI\<traderId>.dlg` **覆盖**包内对话树（`Directory.GetFiles(BaseDir, "*.dlg")`）。我在客户端留了个 325 B 的占位 `.dlg`，结果该商人点"拜访"永远无反应（`[talk] Failed to open visit (button unlocked again...)`），而其它商人正常。

修法：把 `BepInEx/config/VisitAPI` 加进 `syncPaths`（整目录校验，客户端多余文件会被清掉），并把 4 GB 的 `rooms` 加进 `exclusions`。

---

## 10. S9 大依赖升级导致存档被清（流程事故）

### 10.1 事故

2026-11-14 17:06:06–17:06:08，存档 **375 件物品被删**。我在 WTT-ContentBackport 2.0.5→2.0.7 换到一半时启动了服务端：

```
[Error][ModValidator] 模组 Manimal-Halloween2026 需要 com.wtt.contentbackport，版本>=2.0.7。
当前已安装版本是2.0.5
模组发现错误，重启服务器以及修复问题之前不会加载任何模组
```

⇒ 全部 mod 不加载 ⇒ `ProfileValidatorHelper` 把查不到模板的物品全部清理。缺失模板：`68badb6458dbe419740b1520`、`6a16dc8b12144b7727017dad`、`6a3e92fdb0de53ae72082abb`、`68c2940aecc41cc5490bd40e`。

### 10.2 恢复

服务器无卷影副本 ⇒ 用 `SPT_Runtime\user\profiles\backups\2026-11-14_08-42-55\` 整档覆盖（丢 24 分钟进度），先把现状备份到 `缓存区\备份\profile_before_restore_20261114_1935`。

### 10.3 铁律（写进流程）

```
① 停服
② 换依赖（先装新依赖，再换依赖它的 mod）
③ 启动并确认日志里没有 ModValidator 报错、mod 数量正常
④ 才允许开服给人进
```

---

## 11. 部署矩阵与自查命令

### 11.1 五端一致性自查

```powershell
$ends = @{
 'client'  = 'F:\EFT'
 'server'  = '\\Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-SERVER'
 'head0'   = '\\Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-headless'
 'head1'   = '\\Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-headless1'
 'head2'   = '\\Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-headless2'
}
foreach($k in $ends.Keys){
  $root = Join-Path $ends[$k] 'BepInEx\plugins'
  foreach($n in 'Fika.Core.dll','FikaInventoryResync.dll','DshReconnectResync.dll','LateJoin.Client.dll'){
    $f = Get-ChildItem -LiteralPath $root -Recurse -File -Filter $n -ErrorAction SilentlyContinue | Select-Object -First 1
    if($f){ '{0,-7} {1,-26} {2}' -f $k,$n,(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.Substring(0,16) }
    else  { '{0,-7} {1,-26} 缺失' -f $k,$n }
  }
}
```

### 11.2 客户端↔无头 插件目录差集

```powershell
$c = Get-ChildItem 'F:\EFT\BepInEx\plugins' -Directory | Select-Object -Expand Name
$h = Get-ChildItem '\\Xtzj-20200101vo\c$\Users\Wood\Documents\EFT-headless\BepInEx\plugins' -Directory | Select-Object -Expand Name
Compare-Object $c $h | Format-Table -AutoSize
```

### 11.3 日志看哪里

| 内容 | 位置 |
|---|---|
| 服务端 | `EFT-SERVER\SPT_Runtime\user\logs\spt\spt<日期>.log`（UTC，本地 +8） |
| 客户端 BepInEx | `F:\EFT\BepInEx\LogOutput.log` |
| 客户端 Unity | `C:\Users\<user>\AppData\LocalLow\Battlestate Games\EscapeFromTarkov\Player.log` |
| 无头 BepInEx | `EFT-headless*\BepInEx\LogOutput.log`（上一份是 `LogOutput_prev.log`） |
| 局内事件 | `SPT_Runtime\user\logs\HeadlessPanel\raids\<实例>\*.jsonl` |

---

## 12. 经验卡索引（procedures/）

| 卡片 | 何时读 |
|---|---|
| `编译-Fika改源码.md` | 重新编译 Fika.Core（hollowed.dll 必须 upstream） |
| `诊断-卡手打不开背包切不出武器.md` | 背包锁死/捡不起东西的诊断链 |
| `诊断-无头缺插件导致的报错.md` | 换无头就崩 / `Requested value ... was not found` |
| `补丁-TypeByName静默失败.md` | Harmony 补丁"看起来生效其实没生效" |
| `部署-NarcoNet分发.md` | 往服务端 plugins 加/换文件后 |
| `部署-NarcoNet撤同步后客户端报重复键.md` | 撤销 syncPaths 后客户端起不来 |
| `部署-备份放错位置等于没移除.md` | 停用 mod 的正确姿势 |
| `位移-位置被拉回.md` | 重连/位置还原类问题 |
| `排查-深入调查要跑完整个链路.md` | 判定"根因"之前必须走完链路 |

---

## 附录 A：日志关键字速查

```
[FikaInventoryResync] v1.3.2 loaded ...          ← 库存补丁生效
[Resync] patched FikaServer.OnReconnectPacketReceived   ← 重连补丁生效
[Resync] 已补发 OwnCharacter -> <profileId>      ← 重连位置兜底触发
[Resync] manual resync: localCleared=N ...       ← End 键手动重同步
Cloned item ID desync. Expected ID ... real ID ...   ← id 分叉（Patch 4 针对的）
Not enough space in target (122)                 ← 重复投递的典型报错
item X is not located at <A>. It's at <B> (120)  ← 重复投递/镜像分叉
PlayerIsBusyError: Cannot apply ...              ← 残留 inventory lock
no player with id N for packet type HealthSync   ← 中途加入时序
An attempt was made to transition a task to a final state ...   ← 无头 raid-init 双完成
[LocationFix] start location corrected: ...      ← 地图纠正（转移局不应出现）
```

## 附录 B：源码位置

```
工作区\fika-2.4.6-patched\            Fika.Core 源码（4 项补丁）
工作区\fika-inventory-resync\         FikaInventoryResync v1.3.2
工作区\dsh-reconnect-resync\          DshReconnectResync 1.2.0
工作区\dsh-mapvariants-headless\      DshMapVariantsHeadless
工作区\fika-location-fix\             FikaLocationFix（未部署）
工作区\fika-loading-prestige\         加载页名字右侧显示声望图标
工作区\headless-panel\                无头启停面板（独立仓库）
```

## 附录 C：备份与回滚

- 备份根目录：`缓存区\备份\`
- 涉及本次同步体系的备份示例：`fika_jsonc_20261027_*`、`narconet_cfg_*`、`profile_before_restore_20261114_1935`、`terminal_fallingskies_20261118`、`ticket_locale_cond_20261120`
- 回滚原则：**删插件 DLL 即完全还原**（所有自研插件都是纯 Harmony 运行时补丁，除 `Fika.Core` 源码补丁需换回原 DLL）
