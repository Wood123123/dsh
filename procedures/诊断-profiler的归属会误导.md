# 诊断-profiler的归属会误导

> **触发**：用 `maschine-ModProfiler`（或类似工具）的 CSV 判断"哪个 mod 慢"，得出某个 mod 占 12% 之类的结论
> **验证**：拿那条记录去 [TARGET] 里核 —— 目标类型是**游戏的**还是**mod 的**

## 首选

**读 CSV 的正确姿势：看 `method` 字段里的箭头，不要看 `mod` 字段。**

```
"InterchangeRework",Coroutine,"AudioCullingRepair.Work (coroutine/async)",972.1,59075,0.0165
                      ^^^^^ 这是【补丁类所在程序集】    ^^^ 这是被 hook 的【目标方法】

同一份 CSV 里还有配套的一条:
"InterchangeRework",Harmony,"AudioCullingRepair.Prefix (prefix → AudioSourceCulling.Work)",0.0,0,0.0000
                                                              ^^^^^^^^^^^^^^^^^^^^ ★ 目标在游戏程序集里

⇒ 那 972 ms 是【游戏的 AudioSourceCulling.Work】，不是 InterchangeRework 的代码
```

**判据：**

| 看的 | 说明 |
|---|---|
| `mod` 字段 | **补丁类**所在的程序集 —— 只说明"谁动了它" |
| `method` 里的 `→ X` | **被 hook 的目标** —— 这才是真正跑的东西 |
| `category` = `Coroutine` / `Unity` | 目标多半是**游戏**代码，mod 只是加了钩子 |
| `category` = `Harmony` | 目标是 mod 自己的补丁方法 |

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 直接按 `mod` 字段分组求和 | 会把**游戏代码的开销**算到 mod 头上（我这次就错了） |
| 看到 `mod=Fika.Core` 就以为"Fika 慢" | 那可能全是游戏侧被 hook 的 Update |
| 用 CSV 找"该优化哪个 mod" | CSV 回答"哪段时间花在哪"，不回答"谁写的这段代码" |

## 注意

- **静态扫描和 CSV 各错一半**：
  - **CSV** 的坑是**归属**（谁写的）—— 本条讲的就是它
  - **静态扫描**的坑是**可达性**（会不会执行）—— 见 `诊断-静态扫描必须核守卫`
- **正确做法**：CSV 定位"哪条路径贵" → 反编译**目标类型**看它是不是 mod 的 → 用静态扫描确认 mod 那部分有没有守卫
- **`Coroutine` 类别的名字尤其容易误读** —— profiler 常拿补丁类名去标注游戏协程

证据：2026-10-16 会话；`maschine-ModProfiler` 的 `modprofiler-*.csv` +
Cecil 反编译 `Assembly-CSharp.AudioSourceCulling`