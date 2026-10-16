# 补丁-TypeByName静默失败

> **触发**：Harmony 补丁代码写完、编译通过、加载无报错，但功能**毫无变化**
> **验证**：启动后看日志里**自己打的**自检行（`Harmony.GetPatchInfo` 回查），不是看有没有报错

## 首选

1. **优先用编译期类型引用**（`typeof(X)`），不要在能引用时用字符串查找
2. 必须字符串查找时，用**完整命名空间 + 类名**，并**回查确认挂上了**：

```csharp
var t = AccessTools.TypeByName("完整.命名空间.类名");
if (t == null) { Log.LogWarning("[自检] 找不到类型 —— 补丁未装"); return; }

new Harmony(Guid).Patch(target, prefix: new HarmonyMethod(...));

// ★ 回查：HarmonyX 会静默失败
var info = Harmony.GetPatchInfo(target);
bool hit = info?.Prefixes.Any(p => p.owner == Guid) == true;
Log.LogInfo(hit ? "[自检] ✓ 已挂载" : "[自检] ✗ 目标存在但补丁未挂上");
```

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 用 `asm.GetTypes()` 拿 `t.Name == "完整.命名空间.类名"` | `t.Name` 是**简单名**（`FikaHistory`），永远不等于全名 ⇒ 静默不匹配 |
| 只在加载时查一次类型 | BepInEx **不保证插件加载顺序** ⇒ 依赖的 dll 可能还没加载 |
| 相信"没报错就是成功了" | HarmonyX 大量失败路径是静默的 |

## 注意

- **加载顺序是第二个坑**：把它和这个合并处理 —— 用**延迟重试**（`Update()` 里每 2 秒重试，最多 ~60 次）
- 本环境的实证：`ShiJiuReconnectFix` 因 `t.Name` 比全名，**从未生效过**；
  日志只有 `[ShijiuFix] ★ 重试 60 次后仍未找到拾玖的 Fika 类型 —— 插件无效`
- 判据：**每个补丁都要有 `Harmony.GetPatchInfo` 自检行**，没有自检的补丁不算装上了

证据：2026-10-15 会话；`工作区\_shijiufix\ShijiuReconnectFixPlugin.cs`