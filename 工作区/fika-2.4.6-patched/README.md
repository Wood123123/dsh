# fika-2.4.6-patched — 打了补丁的 Fika.Core 2.4.6

> **状态**：已编译并部署到四端（无头端0 等重启生效）
> **基线**：`project-fika/Fika-Plugin` tag **v2.4.6**（`9572504 Fix nullref on death`）
> **产物**：`Fika.Core.dll` v2.4.6.0，1,973,248 B
> 上游原始 README 见 `README-upstream.md`

---

## 一、为什么有这个项目

原来靠 `FikaInventoryResync` 插件打 Harmony 补丁修失同步。三个问题：

1. **网络层根因只能靠 Transpiler** —— 依赖 `OnNetworkReceive` 的精确 IL 结构，Fika 一更新就坏
2. **Fika 2.4.6 并未修复「捡不起东西」** —— 对比 2.4.5 只改了 3 个文件，全是 `Fix nullref on death`
3. 那个修复只存在于 **Fika 3.0.1（SPT 5.0 / IL2CPP 移植版）**，而它是改源码实现的

⇒ **直接改 Fika 源码**，把 3.0.1 的修复按 4.x / Mono 的 API 移植过来。
比外部 Harmony 补丁稳得多，也不会有补丁间冲突。

---

## 二、四项修复

### ① 网络层：库存操作不再被投递两遍（根因）

**文件**：`Fika.Core\Networking\FikaServer.cs`

原来两条转发路径叠加：

```csharp
// 路径 A（原始裸转发，主机校验【之前】就发）
if (reader.GetByte() == 1)
    _netServer.SendToAll(reader.GetRemainingBytesSpan(), deliveryMethod, peer);

// 路径 B（FikaServer.Callbacks.cs:888，主机校验【成功】后）
SendGenericPacket(in packet, DeliveryMethod.ReliableOrdered, true, peer);
```

⇒ **每个非发起方客户端收到两份完全相同的库存操作，重放两次。**

第二次必然失败：
`Not enough space in target` / `item X is not located at <A>. It's at <B>` / `Could not find item owner with id`

而这个失败会触发**权威库存替换** ⇒ 背包界面清空 ⇒ **本局再也捡不起东西**。

而且路径 A 会把**主机拒绝**的操作也泄漏给其他客户端（路径 B 的失败分支是 `return`，不转发）。

**修复**：路径 A 跳过 `InventoryOperation`，只保留路径 B 的「校验后单路转发」。

```csharp
if (reader.GetByte() == 1)
{
    if (!IsInventoryOperationPacket(reader))     // ★ 新增：peek 不消费
        _netServer.SendToAll(reader.GetRemainingBytesSpan(), deliveryMethod, peer);
}
```

新增 `IsInventoryOperationPacket(NetPacketReader)`：存 `Position` → 读 `EPacketType` / `EGenericPacketType` → `finally` 恢复 `Position`。任何失败路径返回 `false`（行为退回原样）。

> **等价于 Fika 3.0.1 的 `if (subType == EGenericPacketType.InventoryOperation) relay = false;`**
> 但这里是**源码级**的，不依赖 IL 结构。

### ② 逃生舱：`NativeInteractionRelease`（新文件）

**文件**：`Fika.Core\Main\Utils\NativeInteractionRelease.cs`

拾取走 `EFT.PickUpState`（`EPlayerState.Pickup`）+ 动画交互队列。
当驱动它的库存操作始终没有回报终态时，状态机**永久停在 PickUpState**：

- 视角不能转、不能移动、不能蹲
- `MovementContext.CanInteract` 一直返回 `"Can't interact in Pickup"`
- ⇒ 交互提示不再出现 ⇒ **地面战利品再也捡不起来**
- 但库存和开火还能用（所以看起来「人能走」却「捡不起东西」）

`Release(player, reason)` 按顺序做（**每步独立 try/catch**）：

```
AnimatedInteractions.ForceStopInteractions()
MovementContext.StopAnyInteractions()
MovementState.Cancel()                                  ← 需 as 转换
BaseMovementState.Exit(false)
MovementContext.ProcessStateEnter(States[EPlayerState.Idle])
MovementContext.SetInteractInHands(EInteraction.None)
MovementContext.PlayerAnimatorSetInteraction(false, false)   ← 两个参数！
MovementContext.ToggleBlockInputPlayerRotation(false)
Player.InteractableObject = null
Player.ForceInteractionsChanged()
```

**只恢复表现层状态，绝不触碰物品 / 地址 / 操作** ⇒ 不会导致复制或回滚。

`IsStuck(player, out detail)` 判定：`CurrentState ∈ {Pickup, Loot, DoorInteraction, BreachDoor, Plant}`
**或** `CanInteract != null` **或** `AnimatedInteractions` 卡住。三者都不成立就什么都不做
（避免打断合法的换弹 / 开门 / 检视 / 投掷）。

### ③ 看门狗：`PickupStuckWatchdog`（新文件）

**文件**：`Fika.Core\Main\Utils\PickupStuckWatchdog.cs`
**挂载**：`Fika.Core\Main\Components\CoopHandler.cs` 的 `Update()`（host / client 都有）

**为什么不能只在「操作失败」时释放**：状态可能比一个*报告成功*的操作活得更久。
所以必须独立看门，**观察超时**而不是观察失败。

节流与阈值（照 Fika 3.0.1）：

| 项 | 值 |
|---|---|
| 检查间隔 | 1 秒 |
| Loot / Pickup 超时 | 5 秒 |
| DoorInteraction / BreachDoor | 8 秒 |
| Plant | 20 秒 |
| 修复冷却 | 8 秒 |

**开销**：入口每帧，但只读几个属性、正常时立即 `return`、不分配 ⇒ 亚微秒级。
真正的释放动作只在「卡住 + 超时 + 冷却过后」执行。

### ④ 残留物品栏锁清理

同一看门狗内的 `TryClearStuckInventoryLock`。

`ItemController.Locked`（在基类上：`InventoryController ← PersonItemController ← ItemController`）
只被三个移动状态设置、在它们的 `Exit` 里清除：

```
ApproachState           Enter / Exit
DoorInteractState       Enter / Exit
StationaryPlayerState   Enter / Spawned      ← 注意有 Spawned 这个额外写入点
```

交互被打断（掉线、切图、死亡动画、主机卡顿）时 `Exit` 不会被调用 ⇒ 锁永久挂着。
之后 `ItemController.CheckItemAction` 会抛
`PlayerIsBusyError: Cannot apply {item} because {target} is currently being modified`
⇒ 该玩家之后**一切物品操作全部失败**，直到本局结束。

**判据**：`ItemController.Locked == true` **且** 当前状态名不是那三个 ⇒ 清掉。

⚠ `MovementContext.StateLocksInventory` 是**只写属性（没有 getter）**，读不了，只能用状态名判定。
⚠ 状态为 `null`（加载/卸载中）时不动 —— 那时 `Locked` 可能是引擎的正常中间态。

---

## 三、编译

```powershell
cd Fika.Core
dotnet build -c Release
# → bin\Release\netstandard2.1\Fika.Core.dll
# → Build\BepInEx\plugins\Fika.Core.dll（postbuild 自动拷）
```

**依赖**：`References\hollowed.dll` **必须是 upstream 的**（9,271,296 B）。
用本地 `Assembly-CSharp.dll`（16,233,472 B）会报 **90 个错** —— 那些 private 字段在原始 dll 下不可访问。
其余 28 个 dll 都用本地的（`F:\EFT\EscapeFromTarkov_Data\Managed` + `F:\EFT\BepInEx\plugins\spt`）。

详见经验卡 `procedures\编译-Fika改源码.md`。

---

## 四、部署位置

| 端 | 路径 | 状态 |
|---|---|---|
| Wood 客户端 | `F:\EFT\BepInEx\plugins\Fika\Fika.Core.dll` | ✅ 1,973,248 B |
| 分发源 | `…\EFT-SERVER\BepInEx\plugins\Fika\Fika.Core.dll` | ✅ 1,973,248 B |
| 无头端0 | `…\EFT-headless\BepInEx\plugins\Fika\Fika.Core.dll` | ⚠ 1,972,736 B（中间版）+ `.new` 待生效 |
| 无头端1 | `…\EFT-headless1\BepInEx\plugins\Fika\Fika.Core.dll` | ✅ 1,973,248 B |

（`…` = `\\Xtzj-20200101vo\c\Users\Wood\Documents`）

**备份**：各端 `Fika.Core.dll.bak_20261017_113923`（原 2.4.5）+ `.bak2_20261017_114027`

**无头端0 生效**：进程在跑，dll 被 user-mapped 占用。下次重启前把 `Fika.Core.dll.new` 改名为 `Fika.Core.dll` 即可。
中间版已有 ①②③ 三项（含「捡不起东西」的全部核心修复），只缺 ④。

---

## 五、取代了什么

`工作区\fika-inventory-resync\`（`FikaInventoryResync`）**已从四端 plugins 移除**，
归档在 `<游戏根>\_plugin_backups\plugins_removed_20261017_113932\`。

| FikaInventoryResync 的补丁 | 本项目的对应 |
|---|---|
| Patch 3（Transpiler 改 broadcast 字节） | ✅ ① 网络层（源码级，更稳） |
| LockFix（清 `Locked`） | ✅ ④ |
| Patch 1/2（resync 请求/守卫） | ⚠ 未移植 —— Fika 自己的 resync 路径已存在，且 ① 修掉根因后不再需要 |
| 日志节流 | ✅ ④ 的 `_lastLockReport` 去重 |

**参考来源**：`缓存区\参考源码\_fika5_src\Fika-3.0.1-spt5.0-src\`（SPT 5.0 移植版源码）
