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
