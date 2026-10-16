# TransitDiag —— 中转全链路探针 v1.0.0

**产物**：`TransitDiag.dll` `17,408 B` SHA256 `1F4C01FFE01DDB05…`
**部署**：`BepInEx\plugins\TransitDiag\TransitDiag.dll`（F-EFT 客户端 + EFT-headless）

**日志前缀**：`[TransitDiag]`，全部用 `Warning` 级别，方便过滤。

---

## 探针清单（13 个，全部核对存在）

### 客户端链路

| # | 位置 | 类型::方法 | 它回答什么 |
|---|---|---|---|
| **①** | 收到房主下发 | `Fika...ClientTransitController::OnInitEvent` | 收到事件没？包内 `PlayerId` 是多少？点数几个？ |
| **②** | **ID 匹配** ★ | `EFT.NetworkTransitController::IsTargetPlayer` | **包内 PlayerId vs 我的 PlayerId，匹配否**（原版此处**静默**失败） |
| **③** | 过滤 | `...NetworkTransitController::GetTransitPoints` | 阵营/保护地点过滤后剩几个，明细是什么 |
| **④** | **可见性** ★ | `EFT.ClientTransitController::IsVisibleTransitPoint` | 每个点的 `hideIfNoKey`/`active`/`activateAfterSec` + 判定结果 |
| **⑤** | **互动键生效** ★ | `EFT.ClientTransitController::SetTimers` | **这一步被调用了吗**（没被调用 = 没有互动键） |
| **⑤b** | 重算 | `Fika...ClientTransitController::UpdateTimers` | 定时重算是否在跑 |
| **⑥** | 玩家交互 | `Fika...FikaPlayer::TransitInteraction` | 你踩进区域、按互动键了吗 |
| **⑦** | 收到指令 | `Fika...ClientTransitController::HandleClientExtract` | 房主的 Extract 到了吗 |

### 房主链路

| # | 位置 | 类型::方法 | 它回答什么 |
|---|---|---|---|
| **⑧** | 下发事件 | `Fika...HostGameController::SetupEventsAndExfils` | 房主为**哪个**玩家发事件（PlayerId/NetId/Side） |
| **⑫** | 初始化 | `...FikaHostTransitController::Init` | 房主中转控制器初始化了吗 |
| **⑨** | 房主收交互 | `...FikaHostTransitController::InteractWithTransit` | 房主收到你的交互了吗 |
| **⑩** | 服务端收包 | `...FikaServer::OnTransitInteractPacketReceived` | 交互包到服务端了吗 |
| **⑪** | 客户端收事件 | `...FikaClient::OnTransitEventPacketReceived` | Extract 事件回来了吗 |

---

## 怎么读日志（判断断点）

**在日志里过滤 `[TransitDiag]`，然后按顺序看：**

| 现象 | 结论 |
|---|---|
| **有 ①，但 ② 显示「匹配=False」** | ★★★ **就是 ID 不匹配** —— Fika 的 headless 缺陷。链条在此中断 |
| **有 ①，但**完全没有** ② 这一行** | `IsTargetPlayer` 没被调用（不太可能）→ 查事件订阅 |
| **② 匹配=True，但 ③ 点数=0** | 阵营/保护地点过滤把它们剔了 |
| **③ 有点，但 ④ 全部「⇒ False」** | `IsVisibleTransitPoint` 拦住了 → 看它打印的 `hideIfNoKey` 值 |
| **④ 有 True，但 ⑤ SetTimers 从未出现** | `GetTransitPoints` 之后异常了，或 `SetTimers` 目标写错 |
| **⑤ 出现了，但踩上去 ⑥ 不触发** | 中转点的碰撞/触发器没启用（GameObject 层问题） |
| **⑥ 触发，但 ⑨⑩⑪ 没出现** | 交互包没发出去（网络层） |
| **⑧ 从来没出现** | 房主的 `SetupEventsAndExfils` 没跑 → 事件根本没下发 |
| **全都有，且 ⑦ 出现** | 中转正常，问题在别处（加载/重连阶段） |

---

## 输出样例

```
[Warning:TransitDiag] ② IsTargetPlayer: 包内=2  我=2  ⇒ 匹配=True
[Warning:TransitDiag] ③ GetTransitPoints: side=Usec ⇒ 通过过滤的点数=4
[Warning:TransitDiag] ④ IsVisibleTransitPoint ⇒ True   id=42 →Shoreline hideIfNoKey=False active=True activateAfterSec=60
[Warning:TransitDiag] ⑤ SetTimers 被调用: 点数=4 player=有 arg3=False  ★ 这一步决定互动键是否生效
[Warning:TransitDiag] ⑥ TransitInteraction 触发！transitPointId=42 keyId= NetId=2
```

**失败样例（ID 不匹配）：**
```
[Warning:TransitDiag] ① OnInitEvent 收到事件: 包内 PlayerId=1 点数=4   MyPlayer.PlayerId=3 NetId=3
[Warning:TransitDiag] ② IsTargetPlayer: 包内=1  我=3  ⇒ 匹配=False   ★★★ 不匹配！这里失败会导致后续全不执行、没有互动键
（③④⑤ 全部不出现 → 结构性缺失）
```

---

## 卸载

删掉 `BepInEx\plugins\TransitDiag\` 整个目录即可，**不改动任何 Fika 文件**（纯 Harmony 前缀/后缀，`OnDestroy` 里 `UnpatchSelf`）。

## 重新编译

```
dotnet build C:\Users\Administrator\Desktop\dsh\_transitdiag\TransitDiag.csproj -c Release
```
