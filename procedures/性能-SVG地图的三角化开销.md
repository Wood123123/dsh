# 性能-SVG地图的三角化开销

> **触发**：装了动态地图类 mod（DynamicMaps 等），进图/开图卡顿、加载变慢、内存涨
> **验证**：看插件目录里有没有 `Maps\*\Layers\*.svg` 和 `Layers\SVG\*.svg`

## 首选

**动态地图的开销在【加载与内存】，不在【战斗中】。定位方法：**

```powershell
# ① 看资源体积与数量（SVG 是矢量，必须运行时光栅化）
Get-ChildItem <插件目录>\Maps -Recurse -File | Group-Object Extension |
  ForEach-Object { '{0} {1} 个 {2:N1} MB' -f $_.Name, $_.Count, (($_.Group|Measure-Object Length -Sum).Sum/1MB) }

# ② 反编译看用了哪个 SVG 库
#    Unity.VectorGraphics  ⇒ 重量级，会做三角化 + 生成纹理
#    自己写的 SVG 解析器   ⇒ 看有没有顶点预算重试
```

**优化（按收益排序）：**

```
① 删掉不玩的地图目录    Maps\<图名>\ 每张独立，删掉不影响其它
② 删掉 Layers\SVG\ 子目录   每张图省一份"完整版"SVG（分层版才是实际用的）
   ★ 先确认日志里的 "Precaching sprite" 用的是 Layers\*.svg 而不是 Layers\SVG\*.svg
③ 关掉 Replace Map Screen   不替换游戏默认地图界面
④ 减少开启的标记类型      不产生每帧开销，但减少 UI 元素与初次构建成本
```

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 按"每帧 Update 数量"找动态地图的开销 | 实测只有 3 个 MonoBehaviour Update，且都有守卫；`RefreshMarkers` 只在 `ReadConfig` 时调一次 |
| 以为那些 `Show X In Raid` 开关每帧扫描场景 | 它们只在**读配置**时生效 |
| 想"关掉地图标记来提速" | 标记不产生每帧开销，关了战斗帧率不变 |

## 注意

- **根因：Unity 原生不认 SVG，必须运行时光栅化成纹理/mesh**
  - 用了 `Unity.VectorGraphics` 就必然有：SVG 解析 → viewBox 正则 → **自适应三角化** → 纹理
  - 三角化是纯 CPU 几何运算，大图很贵
- **`OverBudgetVertices` 用常量 `65500`** = Unity 的 16 位索引缓冲上限（65535）
  ⇒ 顶点超预算要**降档重试**，一张复杂图可能试多轮
- **缓存通常【无上限】且只在战局结束才清**：`MapCache` 是 `Dictionary`，
  释放点在 `OnRaidEnd` / `UnloadMap`
- **实测体量参考**（DynamicMaps 1.2.1）：
  `Maps\` 3.0 MB / 70 个 SVG；最大 `StreetsOfTarkov.svg` 340 KB、`Customs.svg` 232 KB
  每张图有**两份** SVG（`Layers\*.svg` 分层 + `Layers\SVG\*.svg` 完整）
- **判据**：进图时日志出现 `Precaching sprite: Maps\...\Layers\*.svg`
  ⇒ 那就是三角化在跑，且被协程分摊到多帧（所以表现为"加载慢"而非"卡一下"）

证据：2026-10-16 会话；Cecil 反编译 `DynamicMaps.dll` +
`F:\EFT\BepInEx\plugins\mpstark-dynamicmaps\Maps\` 实测量