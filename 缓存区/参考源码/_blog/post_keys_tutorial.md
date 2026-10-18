# MOD 功能按键速查：那些游戏里根本没告诉你的快捷键
> 装了一堆 MOD 之后，很多功能游戏里根本不告诉你按哪个键。
> 这篇把整合包里**所有不显示在游戏按键设置里的快捷键**整理成一张表，
> 顺便讲清楚为什么它们不显示、以及怎么查自己机器上的真实绑定。

---

# 一、为什么游戏里看不到这些按键

EFT 原版的「设置 → 控制」只显示**它自己注册过**的按键。MOD 加的键不走那套系统，通常有三种情况：

| 情况 | 表现 | 例子 |
| --- | --- | --- |
| **① 只存在 MOD 配置文件里** | 游戏内设置里完全没有 | 战术冲刺的「双击 Shift」 |
| **② 组合键 / 手势键** | 原版设置只能存单键，存不下「双击」 | 双击 Shift、按住 M |
| **③ 只注册在 F12 菜单** | 游戏内看不到，只有 BepInEx 配置管理器里能看到 | 绝大多数 MOD 的开关热键 |

所以「按键没反应」大概率不是坏了，而是**你不知道它绑在哪**。

---

# 二、怎么查自己机器上的真实绑定（两个办法）

## 办法 1：F12 配置管理器（推荐，能直接改）

1. 进游戏（主菜单就行），按 **F12** 打开 **Configuration Manager**
2. 右上角设置里勾上 **Show keybinds**（显示按键）
3. 每个插件展开后，带键盘图标的条目就是热键，点一下可以直接重新绑定

> 如果 F12 打不开，检查 `BepInEx\config\com.bepis.bepinex.configurationmanager.cfg`
> 里的 `Show config manager` 是不是被改了。

## 办法 2：直接看配置文件（最准确，绕过界面）

所有 MOD 的配置都在这个目录：

```
<游戏根目录>\BepInEx\config\<插件GUID>.cfg
```

用记事本打开，搜 `Key`、`Hotkey`、`Shortcut`、`Toggle` 之类的词。例如：

```ini
# com.fromhell.tacticalsprint.cfg
[Input]
SprintKey = LeftShift
HoldToSprint = false
DoubleTapSeconds = 0.4
```

```ini
# com.maschine.QuestMarkers.cfg
[General]
Toggle key = Mouse2
```

**注意**：`Mouse0` = 左键，`Mouse1` = 右键，`Mouse2` = **中键**。这是 Unity 的键码编号，不是「第几个键」。

---

# 三、按键总表

> 下表按用途分组。`来源` 一列是对应的 MOD，改键时用它去找 F12 里的位置。

## 3.1 移动 / 战斗

| 按键 | 功能 | 来源 MOD |
| --- | --- | --- |
| **双击 Shift** | **战术冲刺**（速度 ×2，体力消耗 ×3） | FROM HELL - Tactical Sprint |
| **按住 M** | 偷看地图（松开关闭） | Dynamic Maps |
| **K** | 快速装填弹匣 | Continuous Load Ammo |
| **T** | 关手电（原版按 T 是会切战术设备，这个 MOD 顺带关灯） | TDW |
| **V** | 倒地观战视角切换 | Fika Downed Spectator |

**战术冲刺的三个可调参数**（在 `com.fromhell.tacticalsprint.cfg`）：

```ini
[Input]
SprintKey      = LeftShift   # 触发键
HoldToSprint   = false       # false = 双击触发；true = 按住触发
DoubleTapSeconds = 0.4       # 双击判定窗口（秒）

[Movement]
SpeedMultiplier   = 2        # 速度倍率
StaminaMultiplier = 3        # 体力消耗倍率
BreakSpeedLimit   = false    # 是否突破原版速度上限
```

**背重物时战术冲刺会被禁用**（`com.local.tacticalsprint.weightgate.cfg` 的 `Gate = Encumbered`）。

## 3.2 界面 / HUD

| 按键 | 功能 | 来源 MOD |
| --- | --- | --- |
| **鼠标中键** | **开 / 关任务标记 HUD** | Quest Markers |
| **F11** | 开 / 关性能分析浮层 | Mod Profiler |
| **F9** | 开 / 关帧时统计浮层 | EternalCycleFix |
| **F9** | 开 / 关 UWB 定位 | UWB Spike |
| **F7** | 开 / 关赛季契约浮层 | Contagion |
| **F5** | 打开对局回顾网页 | Raid Review |

> ⚠️ 这三个浮层都吃 **F9**，同一时间只有一个能响应。

## 3.3 物品栏 / 价格提示（主要在仓库和交易界面）

| 按键 | 功能 | 来源 MOD |
| --- | --- | --- |
| **鼠标左键拖拽** | 框选多个物品 | Tyfon UIFixes |
| **O** | 打开物品的快捷菜单 | Tyfon UIFixes |
| **T** | 补满弹药 | Tyfon UIFixes |
| **LeftAlt** | 显示物品名称（交易界面） | Caliber Under Name |
| **LeftAlt** | 切换价格显示模式 | Show Me The Money |
| **LeftControl** | 显示弹药详细数据 | Eternal HUD |
| **LeftShift** | 显示物品价值 | Eternal HUD |
| **L** | 显示「安放物品」任务数据 | Eternal HUD |
| **P** | 显示制作配方数据 | Eternal HUD |
| **T** | 显示交易来源数据 | Eternal HUD |
| **F10** | 强制刷新所有物品价格 | Quick Price Lite |

> ⚠️ `T` 在物品栏里同时被 **Tyfon 补弹** 和 **Eternal HUD** 占用；
> `LeftAlt` 被 **Caliber** 和 **ShowMeTheMoney** 占用。冲突时以最后加载的为准。

## 3.4 地图（Dynamic Maps）

| 按键 | 功能 |
| --- | --- |
| **M（按住）** | 偷看地图 |
| **分号 `;`** | 地图居中到玩家 |
| **End** | 开 / 关小地图 |
| **小键盘 8** | 小地图放大 |
| **小键盘 5** | 小地图缩小 |
| **左方向键** | 地图左移 |
| **句号 `.`** | 切换地图楼层 |
| **减号 `-`** | 缩小地图 |

## 3.5 联机（Fika）

| 按键 | 功能 | 说明 |
| --- | --- | --- |
| **F8** | 手动撤离 | 只在主机允许时有效 |
| **F9** | 自由相机 | 调试用 |
| **RightControl** | 聊天 | |
| **End（按住 3 秒）** | **倒地后放弃、直接死亡** | 必须按住，松手会取消 |
| **End（点一下）** | **手动解卡** | 活着时按，见第四节 |

## 3.6 调试 / 诊断

| 按键 | 功能 | 来源 MOD |
| --- | --- | --- |
| **F12** | BepInEx 配置管理器 | Configuration Manager |
| **F6** | SAIN 编辑器 | SAIN |
| **F7** | 身体诊断探针 | Manimal Icebreaker × Fika |
| **F9** | 帧时统计 | EternalCycleFix |
| **小键盘 1** | 中转点信息面板 | Transit Overlay |
| **小键盘 2** | 世界坐标标注 | Transit Overlay |
| **小键盘 3** | 导出坐标到文件 | Transit Overlay |
| **小键盘 4** | 碰撞盒线框（穿墙可见） | Transit Overlay |

---

# 四、重点：`End` 键被绑了 4 次

这是整合包里**最容易出事**的一个键。查配置文件可以确认：

```ini
# com.fika.core.cfg
[Coop | Revival]
Give Up Key = End                    # 倒地时：按住 3 秒放弃

[Coop | Stuck Recovery]
Release Stuck Hands Key = End        # 活着时：点一下手动解卡

# com.lacyway.hanb.cfg
[Keybind]
Reset Key = End                      # 重置手部控制器

# com.mpstark.dynamicmaps.cfg
[5. Mini-map]
Show or Hide the mini-map = End      # 开关小地图
```

## 它们为什么不会互相打架

因为**生效条件不同**：

| 状态 | 响应哪些 |
| --- | --- |
| **活着、正常游玩** | 手动解卡 + 重置手部 + 开关小地图（三个同时） |
| **倒地濒死** | 只有「按住 3 秒放弃」；解卡会自动跳过（不做任何干预） |
| **已死亡** | 都不响应 |

**「按住 3 秒放弃」用的是按下开始计时、松手取消**，所以点一下 End 不会误自杀。

## 但「活着时」确实三个同时触发

按一下 End，会同时：开/关小地图、重置手部、执行手动解卡。前两个是**副作用**，平时看不出来，但如果你在关键时机按了 End 查卡，小地图会顺带被切走。

**建议**：把「手动解卡」改成不常用的键（比如 `Delete` 或 `F10` 之外的键），避开 End。改法见第五节。

---

# 五、怎么改键

## 情况 A：键在 F12 里能看到

直接按 F12 → 找到对应插件 → 点那个按键条目 → 按新键。

## 情况 B：键只在 cfg 文件里

关掉游戏，用记事本打开对应的 `.cfg`，改等号右边的值，然后重开游戏。

值的写法（Unity 键码）：

| 想按的键 | 写成 |
| --- | --- |
| 字母 A | `A` |
| 数字 1 | `Alpha1` |
| 小键盘 1 | `Keypad1` |
| 左 Shift | `LeftShift` |
| 左 Ctrl | `LeftControl` |
| 鼠标中键 | `Mouse2` |
| 鼠标侧键（后退） | `Mouse3` |
| 鼠标侧键（前进） | `Mouse4` |
| 方向键 | `UpArrow` / `LeftArrow` 等 |
| 空格 | `Space` |
| 回车 | `Return` |

## 情况 C：组合键（比如双击 Shift）

可以，但只能改 `DoubleTapSeconds`（判定窗口）。
觉得双击触发不了，把 `0.4` 调大到 `0.5`；觉得太容易误触发，调小到 `0.3`。

---

# 六、常见问题

**Q：按了没反应，是不是 MOD 坏了？**
先按 F12 看那个插件是否被禁用。再看 `BepInEx\LogOutput.log` 里有没有该插件的报错。

**Q：按键生效但游戏里没有提示？**
正常。绝大多数 MOD 热键本来就没有 UI 提示，这也是这篇表存在的理由。

**Q：能不能让 MOD 的键显示在游戏内设置里？**
不能。EFT 的按键设置是原版系统，第三方 MOD 注册不进去。除非那个 MOD 的作者专门做了注入（极少数会做）。

**Q：`Mouse2` 到底是哪个键？**
鼠标中键（滚轮按下去）。

**Q：我把 cfg 改坏了怎么办？**
删掉那个 `.cfg` 文件，重开游戏会自动生成默认值。**不要删整个 `config` 目录**，那会把所有 MOD 的配置一起重置。

---

# 七、一句话速查

```
双击 Shift     战术冲刺
鼠标中键       任务标记 HUD
F11           性能分析浮层
F10           刷新物品价格
F9            帧时统计 / UWB / 自由相机（三选一）
F8            手动撤离
F7            契约浮层 / 身体探针
F6            SAIN 编辑器
F5            对局回顾
F12           所有 MOD 配置（万能入口）
M（按住）      偷看地图
K             快速装弹
V             倒地观战
End           小地图 / 手动解卡 / 重置手部 / 按住 3 秒放弃
```

**记不住就记一个：F12。那是所有 MOD 设置的总入口，里面能看也能改。**
