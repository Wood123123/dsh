'
# 补丁-Harmony打不了readonly struct

> **触发**：日志出现 `HarmonyLib.HarmonyException: IL Compile Error (unknown location)`；或 `PatchAll` 整体失败
> **验证**：看自检行 —— 目标类型是 struct 的那些必然失败，class 的能成功

## 首选

**给 class 目标逐个打补丁，用 `PatchAll` 会因一个失败而全部回滚：**

```csharp
// ✗ 一个 struct 目标失败 ⇒ 全部补丁作废
harmony.PatchAll(assembly);

// ✓ 逐个装、逐个 try，各自独立成败
foreach (var (label, target, prefix, postfix) in list)
{
    try { harmony.Patch(target, prefix: ..., postfix: ...); }
    catch (Exception e) { Log.LogWarning($"[自检] ✗ {label} —— {e.Message}"); }
}
```

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 补丁参数改用具体类型（不用 `object`） | **仍然失败** —— Harmony 必须为实例方法生成 wrapper，与参数无关 |
| 不声明 `__instance` | 仍然失败，同上 |
| 用 `PatchAll` 图省事 | 一个失败全部回滚，连能装的 class 补丁也没了 |

## 注意

- **无法修补的目标**：`readonly struct` 的实例方法、构造函数
  本环境实证：`Fika.Core.Networking.Packets.FirearmController.SubPackets.GrenadePacket`
  （`IsValueType=True` + `IsReadOnlyAttribute`）⇒ `Serialize` / `.ctor(reader)` / `Execute` 全部失败
- **可行的绕过**：
  1. **Transpiler** —— 改写方法体 IL，不生成 wrapper（未验证，理论可行）
  2. **换通道** —— 不用那个 struct 传数据，改用能补的 class 承载
  3. 挂**调用方**（如果调用方是 class）
- 判据：自检行里 struct 目标显示 ✗，是**预期行为**，不是配置错误

证据：2026-10-15 会话；`工作区\_cookingsync\`、`缓存区\参考源码\_cooking_src_gh\`
'