> ## ⚠ 已停用（2026-10-17 14:07）
> 用户要求卸载，已从四端 BepInEx\plugins\ 移除，NarcoNet 清单条目已删除。
> 备份：各端 _plugin_backups\removed_AirClimb_20261017_140751\
>
> **停用原因**：三道门（状态入口 / IsGrounded / 跳跃动画）全部拆开后，
> 日志证实空中 Climb 放行了 5 次、Vault 成功 1 次，
> 但用户感知仍是「没成功」——剩余瓶颈是障碍扫描在空中频繁落空（h/l/d = 0.00），
> 不是权限问题。源码留档备查，不再维护。

---
# AirClimb — 空中攀爬

> **状态**：v1.0.0 已编译并部署到四端
> **性质**：纯客户端 BepInEx 插件
> **产物**：`AirClimb.dll` 10,240 B，SHA256 `076844f3af178aaea135b58460688a17fb822255a835b0226d53162892ac71ed`

---

## 一、它做什么

放开 EFT 攀爬/翻越的「必须站在地上」限制，让你在**跳跃中、下落中、跳跃动画进行中**也能触发攀爬。

覆盖三种场景：
- **A** 跳起来够高处的边缘（跳 + 爬）
- **B** 从高处掉下来抓住半空的边缘/梯子
- **C** 从高处跳下踩到墙沿立刻翻上去

---

## 二、原版的三道门（已核对 IL）

`VaultingComponent.CanVaulting()` — 34 指令：

```csharp
if (!_vaultingSettings.IsActive)                            return false;  // a 功能未启用
if (IsVaulting())                                           return false;  // b 已在动作中
if (_vaultingContext.MovementDirection.y >= 0 或 NaN)       继续;          // ③ 向下移动 → return false
else                                                        return false;
if (!_vaultingContext.IsGrounded)                           return false;  // ① 不在地面
if (_vaultingModelDebug.ObstacleCalculatorModel.IsTerrain)  return false;  // c 障碍是地形
return true;
```

`VaultingComponent.GetVaultingStrategy()` — 68 指令：

```csharp
if (!CanVaulting())                                          return None;
isStairs             = StairsCalculatorModel.IsStairsCondition();
isSprintNearObstacle = IsSprintEnabled && BehindObstacleRatio > -0.5f;
if (IsAnimatorInTransitionState(0))                          return None;  // d 动画过渡中
if (PlayerAnimatorIsJumpSetted())                            return None;  // ③ 跳跃动画
if (canVaultByStamina && VaultState.CanMove() && !isSprintNearObstacle && !isStairs)  return Vault;
if (canClimbByStamina && ClimbState.CanMove() && !IsSprintEnabled      && !isStairs)  return Climb;
return None;
```

**本插件放开的就是标 ① ② ③ 的三条。a b c d 和体力/楼梯/疾跑判定全部保留。**

---

## 三、实现方式

两个 `HarmonyPostfix`，**只改返回值，不动任何内部状态**：

| 补丁 | 目标 | 触发条件 |
|---|---|---|
| `CanVaultingPatch` | `VaultingComponent.CanVaulting` | 原返回 `false`，且重新验证 a/b/c 都满足（说明原因只可能是 ①或②）⇒ 按配置改 `true` |
| `GetVaultingStrategyPatch` | `VaultingComponent.GetVaultingStrategy` | 原返回 `None`，且重新验证 ①(新版)/②/③/④/⑥/⑦ 全部满足（说明原因只可能是 ③ 跳跃动画）⇒ 给 Vault/Climb |

**关键设计：先验证其余条件，确认真实原因才放行。** 这样即使 EFT 以后调整判定顺序或新增条件，也不会把「功能被战局限制」「已在翻越中」「障碍是地形」「体力不够」误判成可放行。

### 为什么不需要重做障碍扫描

```
VaultingComponent.TryVaulting():
    Tick()                  ← 更新障碍数据（GridPoints / ObstacleCalculator）
    GetVaultingStrategy()   ← 判定
    DoVaultingByStrategy()
```

**⇒ `Tick()` 在判定之前执行，判定时障碍数据是刚算好的。**（`DoVaultingTick` 在自动翻越开启时也会每帧 `Tick()`。）

---

## 四、配置

`BepInEx\config\com.dsh.airclimb.cfg`

```ini
[Air Climb]

## 总开关。关掉后完全退回原版行为。
Enabled = true

## ★ 放开 ①：不在地面（跳起 / 悬空）时也能触发攀爬/翻越。
## 对应 CanVaulting 里的 !IsGrounded 检查。
Allow While Jumping = true

## ★ 放开 ②：正在下落时也能触发。
## 对应 CanVaulting 里的 MovementDirection.y < 0 检查。
Allow While Falling = true

## ★ 放开 ③：跳跃动画进行中也能触发。
## 对应 GetVaultingStrategy 里的 PlayerAnimatorIsJumpSetted() 检查。
Bypass Jump Animation Gate = true

## 判定日志的最小间隔毫秒数。0 = 每次都记（排障用，会刷屏）。
Log Cooldown (ms) = 1000
```

**⇒ 三个门默认全开。想定位问题时逐个关掉即可。**

---

## 五、兼容性

| 项 | 结论 |
|---|---|
| **Fika** | 不冲突。Fika 不 patch `VaultingComponent`；Scav/PMC 都能触发，操作本身走正常库存路径 |
| **tarkin-ladders** | 不冲突。它 patch 的是 `Physical.CanClimb` / `Physical.CanVault` / `GetAvailableActions` / `ObstacleCalculatorModel.DistanceToMainObstacle` / `MoveInputTranslator.TranslateAxes`，**没有碰 `VaultingComponent`** |
| **无头端** | 已部署但基本不触发（无头端不操作玩家），留着不影响 |
| **Fika 版本** | 无关。打的是 EFT 类型 |

---

## 六、已知风险

```
① 空中时障碍数据可能"扫到了但不可用"
   ⇒ Tick() 确实在判定前跑，但玩家在空中时身体位置与地面时不同，
     射线起点随之改变，可能扫到墙顶而不是墙面 ⇒ 攀爬高度/长度算出 0 或异常值
   ⇒ 表现可能是：原地起跳、翻越距离过短、偶尔瞬移一点

   应对：真出问题时先关 Allow While Falling，只留 Allow While Jumping（最稳）

② 下落中触发可能让你"贴墙滑下"再翻上去
   ⇒ 观感上像空中悬停，不是崩溃

③ 与 Fika 的向量同步无交互
   ⇒ 攀爬是本地移动状态，Fika 会照常同步位置
   ⇒ 但**别的玩家看到你的攀爬动画可能不连贯**（这本来就有，不是本插件引入的）
```

---

## 七、日志

```
[Info : Air Climb] Air Climb v1.0.0 loaded — jumping=True, falling=True, jumpAnim=True
[Info : Air Climb] [AirClimb] CanVaulting patched
[Info : Air Climb] [AirClimb] GetVaultingStrategy patched
[Info : Air Climb] [AirClimb] CanVaulting 放行 (grounded=False, movingDown=True)
[Info : Air Climb] [AirClimb] GetVaultingStrategy 放行 Climb (跳跃动画门已跳过, grounded=False)
```

**⇒ 看到「放行」说明补丁真的在生效；一条都没有说明你没触发到那些条件。**

---

## 八、构建

```powershell
cd src\AirClimb
dotnet build -c Release
# → bin\Release\AirClimb.dll
```

---

## 九、部署位置

| 端 | 路径 |
|---|---|
| Wood 客户端 | `F:\EFT\BepInEx\plugins\AirClimb\AirClimb.dll` |
| 分发源 | `…\EFT-SERVER\BepInEx\plugins\AirClimb\AirClimb.dll` |
| 无头端0 | `…\EFT-headless\BepInEx\plugins\AirClimb\AirClimb.dll` |
| 无头端1 | `…\EFT-headless1\BepInEx\plugins\AirClimb\AirClimb.dll` |

**NarcoNet 已收录**（两份 KnownFiles 都加了条目），客户端会自动收到。
