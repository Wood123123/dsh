# 更新日志（Fika · SPT 5.0.0 血色边缘 / EFT 1.2.0.47888）

本文件把此前分散的 8 份 addendum 合并整理；今后只维护这一个文件。

**版本号规则（2026-10-11 起）**：
- 客户端 `Fika.Core`、Headless `Fika.Headless`、服务端 `fika-server` 各自独立编号，**正式发布时三者成套更新**；
- **测试构建不递增版本号**——一律保持最近一次正式版的编号（当前 **3.0.4 / 1.5.5 / 2.4.5**），测试包之间用 **DLL 的 SHA256 / 构建时间**区分，避免"版本号看着一样其实不是一个构建"的混淆；
- 协议版本（v20/v21…）与显示版本无关，**只在协议真的变了时才抬**，它才是"两端必须同版本"的判据。

---

## 3.0.4 / 1.5.5 / 2.4.5 —— 正式版（2026-10-14）

### 相对 3.0.3 累计的修复（本正式版包含的全部轮次）

- **转移**：客机进出转移区的三条互补上报路径 + 几何兜底（转移点可用）；终点站转移；转移倒计时同步给客机（计时面板唤醒）；全队在点内才激活、有人离开暂停、回来继续。
- **撤离**：打绿色信号弹的撤离点在联机下可用（默认一颗弹全队可用，可切 `Coop / Flare Extract: Squad Shares Activation`）；狙击手免疫链与 `ExitActivate` 链都覆盖；Icebreaker 的 Heli 撤离点。
- **终端站/门**：哨兵刷卡事实同步（`Sentry: Sync Card Access`）；密码门码表由主机生成下发、客机用本地原生控制器判对错、主机也能输码。
- **战斗与刷新**：Boss 刷新改回原版机制（数据里列几条就生效几条）；玩家 SCAV 可加入进行中的战局；Kolotun 冷事件状态同步。
- **表现**：开局倒计时与战局内 HUD 复原；过场 CG（主机/客机播放、跳过提示收尾、结尾结算）修复。
- **性能**：单帧网络事件投递上限（默认 96，`Net Maximum Events Per Frame`），去掉 100 ms 级大帧。
- **稳定性**：撤离管理器销毁期 NRE、转移数据链健壮化、地面拾取卡死看门狗与绕过路径、背包操作修复等。

### 报问题时要给的

- `BepInEx\LogOutput.log`（**崩溃/异常后先复制出来**，下次启动会被截断）；
- 崩溃那台的 `事件查看器 → Windows 日志 → 应用程序` 里 `.NET Runtime` 与 `Application Error` 两条，或 `%LOCALAPPDATA%\CrashDumps` 里的 dmp；
- 复现步骤（地图 / 人数 / 主机还是客机 / 动作顺序）。

---

## 3.0.3 / 1.5.4 / 2.4.4 —— 正式版（已发布，测试构建都以它为基线版本号）

主题：**把移植期为了排障叠加上去的开销全部撤掉** + 修掉战局内 UI/倒计时 + 收窄对第三方 mod 的影响。

### 性能（本次最重要的改动）
- 诊断通道彻底降载：此前**每次注入方法进入/离开都写一行**，实测一局 18 分钟写掉 **191.75 MiB / 1,793,951 行**（99.65% 是这种逐调用行），**1,652 次写盘/秒**、进局段 **2,208 行/秒**（约 51–60 行/帧）。
  现在全部逐调用级日志（`InvokerTrace`、`WarmTable`、`PumpTrace`、`MainMenuUI.trace`、`NativeEntryWarmup` 明细）**默认关闭**，由新的 `Coop / Diagnostics Verbose` 开关控制（默认 `false`，可热切换，不需要重启游戏）。
- 诊断文件改为**缓冲写**（16 KB / 500 ms），并新增**会话内 8 MB 上限 + 自动轮转**（`Fika-diagnostics.N.log`），不再出现单会话 190 MB 的日志。
- `FirstChance` 异常探针按**异常族限量**（每族 8 条/分钟，保留首因、计数与崩溃摘要），不再被同一异常刷满。
- 主菜单面板关闭 **`Main Menu Ref Trace`**（默认改为 `false`）：它按引用写诊断并做 `VirtualQuery`，与面板 10 秒刷新周期叠加，是菜单里"每 ~10 秒卡 ~2 秒"的直接来源。
- 保留的取证能力：崩溃轨迹 `Fika-crash.log`（每行一次不缓冲写、1 秒心跳）、`[FAIL]`/`[WARN]`、会话相位行（`SessionPump`/`JoinMatch` 等）不受影响；排障时把 `Diagnostics Verbose` 打开即可恢复逐调用轨迹。
- 效果（同局估算）：日志降到 **约 965 KB / 5,689 行**（行 −99.68%、字节 −99.52%），写盘次数从 1,652 次/秒降到**全程 <100 次**；进局每帧省下约 **0.18–3.60 ms**（占 27.2 ms 帧预算的 0.7%–13.2%）与 0.25–1.15 MB/s 的 GC 压力。

### 战局内 UI
- **双击 O 的撤离点/剩余时间与撤离倒计时恢复显示**（新增 `Countdown: Restore In-Raid HUD`，默认 ON）：此前倒计时主体被跳过，导致 `GameUI` 根对象整局 inactive，撤离计时面板既不显示也没有数据；现在按与游戏相同的调用序列恢复 层级 → `ProfileId` 绑定 → `SetTime` → `Reveal`，并回读 `activeInHierarchy` 打一行结论。
- **开局倒计时可复原**（新增 `Countdown: Play Opening Countdown` 及若干逐语句开关，默认 ON）：倒计时流程改为在托管泵上逐语句执行，**不读引擎 `WaitForSeconds.m_Seconds`、不在 inactive 对象上起协程**，每条语句独立开关 + 轨迹标签，出问题能一条条关回去（详见下方"倒计时语句开关"）。
- **引擎侧协程边界**：inactive 的 UI 对象上不再产生 Unity 红字（`Coroutine couldn't be started … 'TimerPanel' is inactive`），改为一行 `[PanelGuard] refused …` 诊断。

### 稳定性
- **托管等待不再跨进引擎**：新增"值边界"（`EngineRoutineBridge`），托管 `WaitSeconds` 在交给引擎前转换成引擎原生 `WaitForSeconds`，嵌套例程递归转换，其它托管值只等一帧并记一行。修掉了死亡镜头 5 秒等待退化成 1 帧的问题，也消除了 `NotSupportedException: … WaitSeconds cannot be converted directly to an Il2Cpp object`。
- **managed-invoker 收窄**：此前该 shim 是全局钩子，会连带接管**第三方 mod 的注入类型**（实测出现在 `AmandsGraphics`、`ArmorClassIcon` 上）。现在默认只接管 Fika 自己的注入类型，第三方回到 il2cpp 原生 invoker；另加失败安全降级与开关。
- 启动刷屏的 `[NativeEntryWarmup] … the runtime rejects …` **691 行 → 1 行汇总 + 1 行按类型聚合**（明细进诊断文件）。
- `Handle is not initialized`（shim 处理 `Il2CppSystem.Object.Finalize`）**120 行 → 每进程 1 行**，且不再产生 first-chance 事件。
- 关机瞬间的异常洪水（`TaskCanceledException`×8、`IOException`×3、`ObjectDisposedException`×2，共 286 行）**→ 1 行 `shutdown detected`**，探针在 `OnApplicationQuit` 后停止写盘（崩溃轨迹不受影响）。
- `GetPublicIP` 失败降级为一次完整首因 + 每个 provider 一行（`api.ipify.org` 被网络 RST 属常见现象，不是证书或离线问题，备用 provider 会成功）。

### 已知与说明
- 主机/客机必须**同版本同构建**；服务端 `fika-server` 也需同时更新（版本校验会拒绝旧服务端）。
- 战局内帧率：本次发布把"我们自己叠加的开销"清掉后，剩余掉帧来自 Fika 会话同步与 bot AI 等**结构性成本**；若仍觉得低，可用 `Coop / Send Rate`（默认 `Medium` = 20 Hz，可试 `Low` = 10 Hz）权衡带宽与远端插值。

---

## 3.0.2 / 1.5.3 / 2.4.3 —— 上一版（合并整理自 8 份 addendum）

### 崩溃与失败快照
- 定位并修掉战局开始的 .NET fail-fast（`0x80131506`，*attempted to call a UnmanagedCallersOnly method from managed code*）：根因是 Il2CppInterop 为**带 by-ref 参数的注入方法**生成的 invoker IL 通不过 JIT 校验，而 JIT 只在原生→托管回调里运行，被拒即不可捕获地终止进程。修法：删除空的 `ObservedFirearmController.AdjustShotVectors` 覆盖（不再生成该签名入口）、把行为移入只声明 `__instance` 的 Harmony 前缀，并新增 `NativeEntryWarmup`——加载时把每个注入方法的 invoker 在托管上下文里跑一遍，被拒只记录不崩。
- 新增**崩溃轨迹** `BepInEx/Fika-crash.log`：每行一次不缓冲 `WriteFile`，含语句标签、1 秒心跳与存活进程的轨迹尾巴；即使失败快照绕过所有异常处理器，也能在下一局点名最后一步。
- 所有原生→托管边界异常防护化（Update 拆 5 段独立守护、泵 tick 与每个会话步骤分别包住），某一步抛异常只报一次并继续，不再饿死泵。
- 首次机会探针（first-chance fault probe）默认安装，能报出引擎出错时的具体对象（此前是 0xA 裸指针）。

### 会话泵与战局开始
- 新增 **Update 驱动的 `FikaRoutinePump`**：战局开始（`FinishRaidSetup` → `CountdownScreen` → 原生会话协程）不再交给引擎协程包装器（`Il2CppManagedEnumerator`）——那正是引擎处理 0xA 崩溃的根因。
- 倒计时改为**可分诊的 7 步 bisect 结构**；`Skip Countdown Body` / `Skip Post Wait Block` 两个主开关（当时默认 ON）用于先保证"能开局"，逐条复原留给后续版本（本版已复原）。
- 主机 bot 恢复活动（`SessionRun` 能跑到 `SetupEventsAndExfils`，游戏状态置 `Started`）；玩家 tick 失败改为首次全量、之后最多每 5 秒汇总一次。

### 同步与玩法
- 原生基类调用表端到端审计（124/124 命中）：修掉 `System.Action` vs `Il2CppSystem.Action` 方向错误导致的"本地玩家与 bot 卡在空手控制器"（无武器、不能换枪、bot 不动）。
- `NativeBaseInvoker.Get<T>` 不再抛异常（精确匹配 → 基类 → 签名搜索 → 隐式桥接 → 一次性告警 + 中性降级）。
- 护甲伤害补丁重新生效（`NativeDetourScope` 支持 Il2CppInterop 1.6.7 的写回形状）。
- 地面拾取旁路、手雷/烟雾同步、丢弃容器自愈、转移点倒计时、容器搜索后黑物品栏等一批同步问题（见 `docs/spt5-ground-loot-sync-fix.md`）。
- 主菜单在线玩家面板改为**无协程**（Update 驱动），并在名单里包含自己；`Presence` 通知 websocket 重新建立（补丁目标已改名 `EnsureNotificationManagerAsync`）。
- 日志里不再插值 il2cpp 对象（`LogSafe`），修掉 `PetId`/`location.Id` 之类的不可捕获访问违例。

### 倒计时语句开关（本版默认 ON，出问题就一条条关）
`Countdown: Activate Game UI` / `Bind Extraction Timer Panel` / `Deploy Wait` / `Restore Raid View` / `Start Raid View Routine` / `Sync Transit Controllers` / `Dispatch Raid Started Event` / `VOIP Audio Fix` / `Restore In-Raid HUD` / `Play Opening Countdown`（及其 4 个子开关）。
