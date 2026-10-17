# Fika for SPT 5.0.0 "血色边缘" / EFT 1.2.0.47965

Fika（SPT 联机 mod）在 **EFT 1.2.0.47888 + SPT 5.0.0 BE** 上的移植版。除了把上游功能搬过来，这一版重点做了三件事：**战局内 UI/倒计时可用**、**把移植期叠加的调试开销全部撤掉**、**把对第三方 mod 的影响收窄**。

| 组件 | 版本 |
| --- | --- |
| `BepInEx/plugins/fika/Fika.Core.dll` | **3.0.4** |
| `BepInEx/plugins/fika/Fika.Headless.dll` | **1.5.5** |
| `SPT_Runtime/user/mods/fika-server/` | **2.4.5** |

> **正式版 3.0.4 / 1.5.5 / 2.4.5**（2026-10-14）：版本号成套抬高一位；测试构建此后以这套编号为基线，包之间用 **DLL 的 SHA256 / 构建时间**区分，只有正式发布才再成套抬高。**判“两端是否同版本”看的是协议版本，不是显示版本。**
> 协议版本：**v23**（升级时主机与所有客机必须一起换；混版本在握手阶段被拒）。
> **压缩包是扁平的**：解压后下一级直接是 `BepInEx\`、`SPT_Runtime\`、`README.md`、`CHANGELOG.md` —— 全选复制进游戏根目录即可。
> **本版主线**：移植期的诊断/取证设施（诊断日志、崩溃轨迹、首次异常钩子、全部探针、日志归档、内存看门狗）与 20 个排障配置键已全部删除；功能语义未变，详见 `CHANGELOG.md`「正式版 3.0.4 / 测试构建轮次 4」。
> **已包含的功能**：联机转移点可用（客机进出转移区的三条互补上报路径 + 几何兜底 + 绑定自检行）；**打绿色信号弹的撤离点**在联机下可用（默认一颗弹全队可用，可用 `Coop / Flare Extract: Squad Shares Activation` 改成"各自打弹"）。
> 绿弹有两套原生机制，别混：**Ground Zero `Sniper_exit`** = 打弹获得**狙击手免疫**（`PlayersWithImmuneToSniperFireFlareCollector`）才能走出去；**Customs `ex_customs_pmc_flare`** = 打弹把点从 `AwaitsManualActivation` 翻成可用。

---

## 1. 环境要求

1. EFT **1.2.0.47888** 客户端 + SPT **5.0.0 BE**（服务端 `SPT.Server.dll` 的 20261008 或更新构建）。
2. `BepInEx` 用 SPT 自带的那一套（SPTushonka / BepInXDX 6.0.0-be.4，运行时 **.NET 10**）——**不要**自己替换 BepInEx。
3. 客户端必须已经**成功启动过一次**，即 `BepInExinterop` 已生成。判断方法：该目录里有上百个 dll，其中包含 `Assembly-CSharp.dll`。
4. `EscapeFromTarkov_Datail2cpp_dataMetadataglobal-metadata.dat` 必须已被 SPT 启动器打过 SPT-core 补丁（文件以 `AF 1B B1 FA` 开头）。若它还是加密的（`1B 21 F3 69 …`），先正常启动一次启动器与游戏。

## 2. 安装

把压缩包里的两个目录复制到游戏根目录（与 `EscapeFromTarkov.exe` 同级）：

```
BepInEx\plugins\fika\Fika.Core.dll
BepInEx\plugins\fika\Fika.Headless.dll
SPT_Runtime\user\mods\fika-server\FikaServer.dll
SPT_Runtime\user\mods\fika-server\FikaShared.dll
SPT_Runtime\user\mods\fika-server\FikaServer.deps.json
SPT_Runtime\user\mods\fika-server\FikaServer.staticwebassets.endpoints.json
SPT_Runtime\user\mods\fika-server\assets\...        ← 必须一起放，缺了服务端起不来
```

- **主机、所有客机、以及服务端必须是同一套版本**：客户端之间靠 mod GUID + CRC32 校验，不一致会被拒绝进入联机。
- 不想用独立头主机（headless）的，可以删掉 `Fika.Headless.dll`；主机自己开时不需要它。
- 更新时**先关游戏**再覆盖；服务端 mod 覆盖后需要重启服务端。

## 3. 首次使用

1. 启动 `SPT_Runtime\SPT.Server.exe`（首次会导入数据库，约十几秒）。
2. 启动 `SPT_Runtime\SPT.Launcher.exe`，添加服务器 `https://127.0.0.1:6969`（异地联机则填主机的地址），创建/选择档案后进游戏。
3. 主菜单右下应显示 `FIKA 3.0.3 | SPT 5.0.0 (BEM) …`。

## 4. 联机

- **主机**：主菜单 → 逃离塔科夫 → 选角色 → 选地点 → **下一步** → `FIKA 联机游戏模式` → 下一步 → 保险 → 下一步 → 确认 → 右侧 **主持一个行动** → 会话设置 → 开始。
- **客机**：同上流程，在联机界面选择主机的房间 **加入**。
- 异地联机建议先确认双方能互相访问到主机的 6969 端口（端口转发 / 虚拟局域网工具）。

## 5. 主要配置项（`BepInEx\config\com.fika.core.cfg`）

| 键 | 默认 | 说明 |
| --- | --- | --- |
| `Coop / Diagnostics Verbose` | **false** | 逐调用级诊断日志总开关。排障时打开可拿到 `InvokerTrace` 等完整轨迹（会明显增加磁盘写入，卡顿请保持关闭）。 |
| `Coop / Main Menu Ref Trace` | **false** | 主菜单面板的引用追踪。开启会在每次刷新时做 `VirtualQuery`，菜单可能出现每 10 秒一次的卡顿。 |
| `Coop / Countdown: Restore In-Raid HUD` | true | 双击 O 的撤离点/剩余时间与撤离倒计时依赖它；关掉则这些 UI 不显示。 |
| `Coop / Countdown: Play Opening Countdown` | true | 开局倒计时流程总开关。若某局开局出问题，可先置 false（另有一组逐语句子开关）。 |
| `Coop / Send Rate` | Medium (20 Hz) | 主机权威发包频率。网络较差可试 `Low`（10 Hz，省一半状态包，代价是远端插值变粗）。 |
| `Coop / Enable Online Players` | true | 主菜单在线玩家面板。 |
| `Coop / Auto Use Headless` | false | 自动改用可用的 headless 主机开局。 |

> 战局设置里的 `disableLegStamina / disableArmStamina` 会让对应部位耐力不再消耗——这是**设计行为**，不是 bug。

## 6. 出问题时要发什么

请把下面这几个文件打包发给作者（路径以你的安装目录为准）：

- `BepInEx\LogOutput.log`（游戏侧总日志）
- `BepInEx\Fika-diagnostics.log`（若开了 `Diagnostics Verbose` 才有逐调用明细；关着时它只有相位与结论行）
- `BepInEx\Fika-crash.log`（崩溃轨迹，含最后一步与心跳）
- `SPT_Runtime\user\logs\spt\*.log`（服务端）
- 如果是闪退：`事件查看器 → Windows 日志 → 应用程序` 里对应时刻的 `Application Error`（需要"错误模块名称/异常代码/偏移"）与 `%LOCALAPPDATA%\CrashDumps\EscapeFromTarkov*.dmp`

常用判读行：

| 日志行 | 含义 |
| --- | --- |
| `Verified N Fika patches` | 补丁安装数量（本版为 208；明显偏少说明有补丁没找到目标）。 |
| `[NativeEntryWarmup] … warmed for …; 0 rejected` | 注入入口暖机结果，必须是 `0 rejected` 那一句。 |
| `[HudRestore] result: gameUi=… live=True, timerPanel=… live=True` | 战局内 HUD 恢复成功。 |
| `[PanelGuard] refused a coroutine start …` | 引擎试图在已关闭的 UI 上起协程，已被安全拦下（不是错误）。 |
| `[SessionPump] #N step 1/3 … 3/3` | 战局开始流程走完。 |
| `shutdown detected (…)` | 正常退出闸门（避免退出瞬间刷异常）。 |

## 7. 移植版已知限制

- 主机与客机之间的同步（快照/插值/观察者与 bot）本身有 CPU 成本，**客机帧率一定低于单机**；本版已把日志等"自造开销"清掉，剩下的掉帧属结构性成本。想进一步压，可调 `Send Rate`、减少 bot 数量。
- 第三方客户端 mod（画面类、UI 类）与本移植可能互相影响：出现"进图后原生闪退、无任何托管异常"时，先只留 Fika + SPTushonka 最小集复现，再逐个加回。
- Headless 主机需要档案名带 `headless_` 前缀并按服务端提示创建（详见服务端 `fika-server` 的路由说明）。

---

上游项目：Fika（project-fika）。本包是**本地适配移植**，仅用于离线/私有服务器；遇到问题请附上第 6 节的日志。
