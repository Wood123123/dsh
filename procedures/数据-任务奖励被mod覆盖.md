'
# 数据-任务奖励被mod覆盖

> **触发**：数据库里查到某任务的奖励有 X，但游戏里看不到 X；或"这任务不是应该解锁某物品吗"
> **验证**：查完 `SPT_Data\database\templates\quests.json` 后，**必须再查 mod 的覆盖目录**

## 首选

三处都要查，缺一不可：

```powershell
# ① 数据库原始定义
$db = "…\\SPT_Runtime\\SPT_Data\\database"
Select-String -Path "$db\\templates\\quests.json" -Pattern "<questId>"

# ② mod 的任务覆盖（QuestBackport 等）
Get-ChildItem "…\\user\\mods\\QuestBackport\\db\\quests" -Recurse -Filter "*.json" |
  Select-String -Pattern "<questId>"

# ③ 商品解锁关系（真正决定"能不能买"的地方）
$db\\traders\\<traderId>\\questassort.json     # {商品条目ID: 任务ID}
Get-Content "…\\user\\mods\\QuestBackport\\db\\QuestAssortRemovals.json"   # 被移除的绑定
Get-Content "…\\user\\mods\\QuestBackport\\db\\QuestAssortUnlocks.json"    # 被改绑的
```

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 只看 `quests.json` 的 rewards 就下结论 | mod 会**整体覆盖**任务的 rewards 数组，把某条删掉 |
| 认为 `rewards` 里的 `AssortmentUnlock` 决定能否购买 | SPT 里它是**空操作**（`RewardHelper.ApplyRewards` 的 switch 里落到循环末尾）；真正的解锁靠 `questassort.json` |
| 把 `AssortmentUnlock.target` 当商人 ID | 它是**商品条目 ID**（= `items[0]._id`），商人在 `traderId` 字段 |

## 注意

- **三个概念容易混**：
  - `quests.json[].rewards` —— 客户端 UI 显示的奖励（可能被 mod 覆盖）
  - `trader/questassort.json` —— **真正决定商品何时出现在商人列表里**
  - `AssortmentUnlock` 奖励 —— SPT 里**不解锁**，`InventoryController.RedeemProfileReward` 反而把它加进黑名单
- **判据**：想知道"能不能买"，**唯一可靠的办法是查 `questassort.json` + mod 的 Removals/Unlocks**，不是看任务奖励

证据：2026-10-16 会话；QuestBackport 把 AVT-40 的解锁从 `64f5aac4…`（苏共之辉-2）改绑到 `626bd75c…`（踩点行动）
'