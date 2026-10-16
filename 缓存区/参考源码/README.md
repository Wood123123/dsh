# 参考源码（只读）

> 第三方 mod 的反编译源码 / 上游 GitHub 源码 / 官方文档快照。
> **改动这里没有任何意义** —— 这里只是用来「查实现」的。
> 上级索引：`..\..\_索引.md`

## ★ 最常用的两个

### `_fika_src` —— Fika 2.4.5 完整源码（60 MB）

```
_fika_src\Fika-Plugin\Fika.Core\          ★ 客户端宿主/客户端核心（同步、包、补丁点都在这）
_fika_src\Fika-Plugin\Fika.Core\Main\     GameMode / Players / Components / HostClasses / ClientClasses
_fika_src\Fika-Plugin\Fika.Core\Networking\  Packets / FikaClient / FikaServer
_fika_src\Fika-Headless\                  ★ 无头端插件（DestroyGraphics 补丁在这）
_fika_src\Fika-Server\                    服务端 mod
_fika_src\gitbook-wiki\                   官方 wiki 源码
```

**查这些事就读它：**
- 某个东西为什么不同步 → `Main\Components\` 里的 `*Syncer`
- 包结构 / 发了什么数据 → `Networking\Packets\`
- 无头端为什么快/慢 → `Fika-Headless\Patches\DestroyGraphics\`

### `_shijiu_src` —— 拾玖 ShiJiu-Tools 完整反编译源码

```
_shijiu_src\ShiJiu-Tools\StashTools\      ★ 客户端（任务记录、价格、UI、暂停）
_shijiu_src\ShiJiu-Tools.Fika\StashTools\Fika\  ★ Fika 适配层（FikaHistory / FikaPause）
_shijiu_src\ShiJiu-Tools.Server\StashTools\     ★ 服务端（HistoryStore / HistoryRouter）
```

**查这些事就读它：**
- 战绩记录怎么生成的 → `HistoryCapture.cs` / `FikaHistory.cs`
- 服务端为什么拒绝上传 → `HistoryStore.cs` 的 `Join` / `Save`
- 为什么适配没启用 → `FikaPluginAdapter.cs`（版本门）

## 其它

```
_questbackport          QuestBackport 反编译 + db 覆盖文件（改版任务、QuestAssortRemovals/Unlocks）
_sain                   SAIN 反编译
_manimal                Manimal Icebreaker 反编译
_sptweb_decomp          SPTarkov.Server.Web 反编译
_peizhi_decomp          配置道具 反编译
_cooking                CookingGrenades 1.4.2 反编译（dnSpy）
_cooking_src_gh         ★ CookingGrenades 1.4.5 官方 GitHub 源码（作者真实写法）
_icefika_decomp         Icebreaker Fika 反编译
_raidguard_decomp       RaidGuard 反编译
_nekogroupex            NekoParty GroupEX 反编译
_nekoparty_transit      NekoParty Transit 反编译
_wtt / _raidreview      WTT / RaidReview 反编译
_lootnet_src            单文件参考
_quickprice_src         QuickPrice 源码
_reference              单文件参考
_taskitem_gh_files      TaskItemIndicator GitHub 文件
_resync_v130 / _resync_v130_hl / _resync_v131_verify    FikaInventoryResync 历史版本
_skipoffmap_src / _skipoffmap_full                      Off-Map Skip
_tacticalsprint_4013_src / _tacticalsprint_src          战术冲刺源码
_tripwire / _tripwire_src                               绊线源码
_ts_verify / _tsfika_verify / _tsfika_revert_verify      战术冲刺验证
_fika244                Fika 2.4.4 解压（版本对比用）
_fikawiki               Fika 官方 wiki 离线快照
_blog                   社区文章草稿
BepInEx / tools         参考 dll
_nekobeta2              NekoParty Beta2 发行包
transpiler分析\          IL 扫描结果 xml
```

## ⚠ 一个坑

`_shijiu_src\ShiJiu-Tools.Fika\ShiJiu-Tools.Fika.csproj` 里有一处**相对路径引用**：
```
..\..\tools\BepInEx\plugins\ShiJiu-Tools\ShiJiu-Tools.dll
```
这条路径在目录移动后**已失效**。该项目本来也不用于重新编译（只是反编译参考），但如果你要编译它，需要改这个 HintPath。