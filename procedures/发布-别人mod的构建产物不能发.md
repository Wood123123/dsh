# 发布-别人mod的构建产物不能发

> **触发**：准备把工作区里的 mod 发到 GitHub/Gitee，不确定哪个能发
> **验证**：看 `BepInPlugin` 的 GUID —— 不是自己起的名字就是别人的

## 首选

发布前逐个项目查 GUID 和依赖：

```powershell
Get-ChildItem -Recurse -Filter *.cs |
  Select-String -Pattern 'BepInPlugin\(' |
  ForEach-Object { $_.Line.Trim() }
```

**判据三条：**
1. `BepInPlugin` 的 GUID 是**自己起的** → 可以发
2. GUID 是别人的（如 `com.fromhell.tacticalsprint`、`ciallo.inftripwire`）→ **不能发，这是重新编译别人的 mod**
3. 只依赖别人但 GUID 是自己的（补丁/附属插件）→ **能发**，但要在 README 里写明依赖 + 理想情况先跟原作者打招呼

## 别再来

| 弯路 | 为什么不通 |
| --- | --- |
| 看目录名判断（`_tacticalsprint_build` 里有 `.cs`） | 那是**反编译后重新编译**的产物，不是自己的代码 |
| "我能编译所以是我的" | 编译 ≠ 著作权。GUID 和原作者署名才是判据 |
| 把"绕过别人限制"的补丁公开发布 | 例如绕过拾玖的版本门 —— 那是在替别人绕过作者的限制，作者可能明确不希望 |

## 注意

- **发布前必查的三样**：
  1. `BepInPlugin` GUID（是不是自己的）
  2. csproj 里的 `HintPath`（不能有 `F:\EFT\...` 这种硬编码，要改成可覆盖变量）
  3. 源码里的注释/示例（可能含**自己的服务端 IP**、路径等）
- **本工作区的结论**：
  - ✅ 能发：HeadlessPanel / FikaInventoryResync / TacticalSprint-Companions（3 个独立仓库）
  - ❌ 不能发：`_tacticalsprint_build`（FromHell 的）、`_tripwire_build`（ciallo 的）
  - ⚠ 谨慎：`_shijiufikafix`（绕过拾玖版本门）

证据：2026-10-16 会话；`工作区\` 全量扫描