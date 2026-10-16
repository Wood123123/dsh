# _shijiu_src — 拾玖 ShiJiu-Tools 完整反编译源码（只读参考）

> 上级索引：`..\..\..\_索引.md`

## 三个工程

### `ShiJiu-Tools\` — 客户端

```
ShiJiu-Tools\StashTools\
  HistoryCapture.cs        ★ 对局记录生成（Begin / Finish / Interrupted / Persist）
  HistoryNetwork.cs        ★ 上传逻辑（Enqueue / Tick / Send）—— 那条「待重试」日志在这
  HistoryDelivery.cs       ★ join + save 两步提交
  HistoryCoopBridge.cs     联机上下文桥
  HistoryUi.cs             记录 UI
  RaidEarnings.cs          ★ 收益采集（Begin / Finish / End / Fail / Tick）
  RaidStartPatch.cs        GameWorld.OnGameStarted 钩子
  RaidPlayerStartPatch.cs  Player.OnGameSessionBegin 钩子
  RaidFinishPatch.cs       LocalGame.Stop 钩子（★ 联机不触发）
  ...其它：Batch / GridPacking / CompactTooltip / DragPreview / 交易和跳蚤相关
```

### `ShiJiu-Tools.Fika\` — Fika 适配层

```
ShiJiu-Tools.Fika\StashTools\Fika\
  FikaPluginAdapter.cs     ★★ 版本门在这：Awake 里 if (version != 2.4.4.0) return;
  FikaHistory.cs           ★ 订阅 FikaGameEndedEvent 等，驱动 RaidEarnings.Finish
  FikaHistorySession.cs    ★ ActionId = "fika-" + 房间GUID；成员采集
  FikaPause*.cs            联机暂停适配
  FikaAdapterBehaviour.cs  驱动 FikaPluginAdapter.Tick
```

### `ShiJiu-Tools.Server\` — 服务端

```
ShiJiu-Tools.Server\StashTools\
  History\HistoryStore.cs  ★★ Join / Save —— 「行动编号已用于另一条记录」在这
  History\TeamRules.cs     Key(context) = SHA256(provider + "\n" + actionId)
  History\TeamManifest.cs  Registrations: 玩家 → {RecordId, StartedUtc}
  History\RaidRecord.cs    记录模型
  Server\HistoryRouter.cs  /stashtools/history/{query,save,join}
  Server\Metadata.cs       IModMetadata（★ 构造里装 StartupLogDisplay 补丁）
  Server\StartupLogDisplay.cs  服务端已有 Harmony 用法的先例
```

## 查问题时的路标

| 想知道 | 读这里 |
|---|---|
| 为什么战绩是「结果未确认」 | `FikaPluginAdapter.cs`（版本门）+ `HistoryCapture.cs` 的 `Interrupted` |
| 为什么上传一直重试 | `HistoryNetwork.cs` 的 `Send` + `HistoryStore.cs` 的 `Join` |
| 重连为什么产生重复记录 | `RaidEarnings.cs:150` 的 `resume` 只在转图时为 true |
| 任务记录有哪些字段 | `History\RaidRecord.cs` |

## ⚠ 编译注意

`ShiJiu-Tools.Fika\ShiJiu-Tools.Fika.csproj` 里有一处**已失效的相对路径**：
```
..\..\tools\BepInEx\plugins\ShiJiu-Tools\ShiJiu-Tools.dll
```
（目录移动后失效。本项目仅作参考，一般不用于编译）