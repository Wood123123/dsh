# inbox —— 待整理的坑

> 踩到坑的**当时**就往这里记一行。收尾时再挑够格的整理成正式卡。
> **别等收尾** —— 收尾要靠回忆，回忆不可靠。记一行也算数。

格式：`- <现象一句话> → <解法一句话>  （<日期>）`

---

- PowerShell 5.1 里 `foreach (...) {...} | Format-Table` 报「不能使用空管道元素」→ 先 `$x = foreach(...){...}` 再 `$x | Format-Table`  （2026-10-16）
- `Get-Content -Raw` 读 UTF-8 中文脚本被按 GBK 解码，乱码成语法错误 → 加 `-Encoding UTF8`  （2026-10-16）
- DSH 沙箱 workspace-write 下 pwsh 每次启动即以 `0xC0000142` 退出（连 `Write-Output` 也无输出）→ 不是系统故障，切 danger-full-access 即正常  （2026-10-16）
- pwsh 工具里用 `@'...'@` 单引号 here-string 传 Python 代码，代码里的双引号被整段剥掉 ⇒ `SyntaxError` → 改用 `@"..."@`（代码内无 `$` 时安全），或先落成 .py 再执行  （2026-10-15）
- Python 打印中文时 `UnicodeEncodeError: 'gbk' codec can't encode` → 跑之前 `$env:PYTHONIOENCODING="utf-8"`  （2026-10-15）
- openpyxl 写完公式，`data_only=True` 读到 `None`、图表空白 → 公式不会自动算，必须再走 LibreOffice kit `recalculate` 生成缓存值  （2026-10-15）
- openpyxl 往返存盘会丢掉 LibreOffice 写出的条件格式扩展（dataBar / colorScale）→ 存盘前把该区域的 CF 规则整体重建  （2026-10-15）
- **自动清 `ItemController.Locked` 必然误伤** —— 这个锁在一切正常物品操作期间本来就是 true（几百 ms），Plant 合法持锁可达 60s，Loot 搜容器也可能很久。任何"发现就清"或"阈值 N 秒就清"都会打断正在进行的操作，表现为 `Cannot apply item weapon` / 装备失败 / 手卡住。解法：改成玩家按键手动触发（Fika 里是 Coop | Stuck Recovery → Release Stuck Hands Key，默认 End），并加"倒地 `ClientHealthController.Downed==true` 时跳过"，否则会打断"按住 End 3 秒放弃"。

- **Unity 刷屏 `Invalid quaternion rotation` 时先找「未初始化的 Quaternion 字段」** —— C# 的 Quaternion 默认是 (0,0,0,0)，`Quaternion.Inverse((0,0,0,0))` 会除以 0 得到 NaN，写进 `Transform.rotation` / `ConfigurableJoint.targetRotation` 就每帧每对象刷这条警告（实测 RagdollKinetics 一场 Labs 刷 63,360 条、占 Player.log 94%）。另注意 `Quaternion` 没有 `sqrMagnitude`，平方模长用 `Quaternion.Dot(q, q)`。排查顺序：先看 Player.log（BepInEx LogOutput.log 可能一条都没有），再 grep 所有 `.rotation =` / `targetRotation` 赋值点。

- **博客园 MetaWeblog 401 的判别法**：用 `metaWeblog.getCategories` 分别发「空用户名+空令牌」「有效用户名+令牌」两组。前者返回 `请输入用户名`、后者返回 `请配置正确的用户名与访问令牌`，说明**用户名没问题、是令牌被拒**。反过来如果两组都回 `请输入用户名`，是用户名/端点错。端点 `https://rpc.cnblogs.com/metaweblog/<博客名>`，正文 `description` + categories 里带 `[Markdown]` 才会按 Markdown 解析。

- **「不能跑 / 卡在正在使用药物」先看 `MedEffect.Residue` 抛的 `Trying to get parent of an item that doesn't have a parent`** —— `Item.get_Parent()` 是**主动 throw**（CurrentAddress 为 null 就抛），不是空引用。`Residue()` 末尾必然调它去发 `DrainItemEventArgs`，而联机下 Fika 的库存同步可能已把见底的食物移出背包 ⇒ 竞态 ⇒ Effect 状态机中断 ⇒ MedsController 收不了尾。修法两条：① Prefix `MedEffect.Residue`，`MedItem.CurrentAddress == null` 就 return false 跳过；② 手动解卡里补 `HandsController.FastForwardCurrentState()` + `Player.Proceed(false, null)`（库存锁/proceed 回调/movement state 在这场景下都是正常的，光查那三项永远救不回来）。
  **实测确认（2026-10-18）**：卡住后用急救包能恢复 —— 因为 `Player.Proceed(Meds, ...)` 内部会重建 HandsController，正好等价于我们在 `ReleaseNow` 里补的 `hands.FastForwardCurrentState() + player.Proceed(false, null)`。反过来说明根因就是 HandsController/Effect 状态机卡死，不是负重也不是库存锁。

- **`.new` 后缀永远不会自动生效** —— BepInEx 只加载 `*.dll`，工作区里也没有任何脚本会自动改名。所以「部署时文件被占用 ⇒ 放 `.new`」之后**必须**：停进程 → 手工改名 → 重启。实测踩过：无头端0 的 `.new` 放了 5 个多小时、进程重启过一次，仍在跑旧版（现役文件写入时间停在放 `.new` 之前），导致客户端 CRC 与新无头端不一致、别人「创建战局」报 FIKA_MISMATCH。另外 **Fika 的 mod CRC 用的是 CRC32C（Castagnoli，多项式 0x82F63B78），不是 IEEE CRC32** —— 源码 `FikaModHandler.cs:50 CRC32C.Compute(...)`，用错算法会得出「谁的码都不对」的错误结论。
