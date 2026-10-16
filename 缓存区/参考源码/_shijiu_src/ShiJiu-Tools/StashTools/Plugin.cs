using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[BepInPlugin("local.shijiu.tools.spt4", "【拾玖】多项功能优化", "1.0.4")]
[BepInDependency("com.SPT.core", "4.1.6")]
public sealed class Plugin : BaseUnityPlugin
{
	public const string Id = "local.shijiu.tools.spt4";

	public const string Author = "拾玖";

	internal static ManualLogSource Logger;

	internal static ConfigEntry<bool> Enabled;

	internal static ConfigEntry<bool> ProtectFir;

	internal static ConfigEntry<SellMode> MiddleMode;

	internal static ConfigEntry<float> PriceFactor;

	internal static ConfigEntry<FleaPriceBasis> PriceBasis;

	internal static ConfigEntry<KeyCode> SellKey;

	internal static ConfigEntry<bool> DiagnosticMode;

	internal static ConfigEntry<bool> ShowStatus;

	internal static ConfigEntry<bool> SelectInRaid;

	internal static ConfigEntry<bool> HoverPriceEnabled;

	internal static ConfigEntry<bool> HoverPerSlot;

	internal static ConfigEntry<bool> HoverTraderPrice;

	internal static ConfigEntry<bool> HoverEligibilityNote;

	internal static ConfigEntry<bool> AllowNonFirFlea;

	internal static ConfigEntry<bool> HoverNameColor;

	internal static ConfigEntry<bool> HoverPriceColor;

	internal static ConfigEntry<float> HoverDelay;

	internal static ConfigEntry<bool> HoverFleaOffers;

	internal static ConfigEntry<bool> IncludeCustomTraderPrices;

	internal static ConfigEntry<PriceDisplayUnit> HoverPriceUnit;

	private void Awake()
	{
		//IL_0a4e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a55: Expected O, but got Unknown
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Expected O, but got Unknown
		//IL_01ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Expected O, but got Unknown
		//IL_029a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Expected O, but got Unknown
		//IL_06bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c3: Expected O, but got Unknown
		//IL_077f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0786: Expected O, but got Unknown
		//IL_0873: Unknown result type (might be due to invalid IL or missing references)
		//IL_087a: Expected O, but got Unknown
		//IL_0af7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0afe: Expected O, but got Unknown
		//IL_03fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0401: Expected O, but got Unknown
		//IL_0453: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Expected O, but got Unknown
		//IL_0949: Unknown result type (might be due to invalid IL or missing references)
		//IL_0950: Expected O, but got Unknown
		//IL_0617: Unknown result type (might be due to invalid IL or missing references)
		//IL_061e: Expected O, but got Unknown
		Logger = ((BaseUnityPlugin)this).Logger;
		Version version = Chainloader.PluginInfos["com.SPT.core"].Metadata.Version;
		if (version.Major != 4 || version.Minor != 1 || version.Build != 6)
		{
			Logger.LogError((object)("此独立移植版仅适配 SPT 4.1.6，当前版本为 " + version?.ToString() + "，未安装任何功能补丁。"));
			return;
		}
		Logger.LogInfo((object)"【拾玖】多项功能优化 1.0.4 | by: 拾玖");
		Logger.LogInfo((object)"4.1.6 社区版：Fika 战績结束事件及暂停加载同步修复。");
		ChineseSettings.MigrateConfig(((BaseUnityPlugin)this).Config);
		Enabled = ((BaseUnityPlugin)this).Config.Bind<bool>("01 基本设置", "启用", true, ChineseSettings.ToggleDescription("框选与批量移动支持仓库、背包、胸挂及打开的容器。快速出售仅限局外。"));
		SellKey = ((BaseUnityPlugin)this).Config.Bind<KeyCode>("01 基本设置", "售卖快捷键", (KeyCode)113, new ConfigDescription("点击按键名称后按下新键，退出键取消。按住此键：中键按所选模式出售，左键卖商人，右键上架跳蚤。", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				CustomHotkeyDrawer = ChineseSettings.DrawKey
			}
		}));
		MiddleMode = ((BaseUnityPlugin)this).Config.Bind<SellMode>("02 出售", "中键出售模式", SellMode.仅跳蚤, "默认仅跳蚤：只上架跳蚤市场，不能上架则保留。收益优先：比较商人收益与跳蚤扣税后的收益；仅商人：只出售给商人。");
		SelectInRaid = ((BaseUnityPlugin)this).Config.Bind<bool>("01 基本设置", "对局内启用框选", true, ChineseSettings.ToggleDescription("与旧版默认行为一致，允许在对局背包界面框选、批量移动已搜索的物品。对局内不提供快速出售。"));
		ProtectFir = ((BaseUnityPlugin)this).Config.Bind<bool>("02 出售", "保护战局内发现物品", false, ChineseSettings.ToggleDescription("启用后跳过带勾物品；按住左换档键可临时放行。"));
		AllowNonFirFlea = ((BaseUnityPlugin)this).Config.Bind<bool>("02 出售", "允许非战局发现物品上架", false, ChineseSettings.ToggleDescription("默认关闭，遵循服务器原始上架规则；服务器本来允许无勾物品时，关闭此项仍可上架。开启后临时放宽游戏的战局发现限制，手动和快捷上架统一生效；关闭后恢复原规则。不会修改物品带勾状态，等级、禁售、费用和栏位限制仍保留。切换后请关闭并重新打开上架窗口。"));
		PriceBasis = ((BaseUnityPlugin)this).Config.Bind<FleaPriceBasis>("02 出售", "跳蚤定价基准", FleaPriceBasis.平均价, "选择游戏接口返回的最低价、平均价或最高价。默认平均价；不是历史成交均价，不保证成交。所选基准无有效报价时不改用其他基准。");
		PriceFactor = ((BaseUnityPlugin)this).Config.Bind<float>("02 出售", "跳蚤价格系数", 1f, new ConfigDescription("上架单价＝所选跳蚤定价基准×系数，四舍五入为整数卢布。1 为原价，0.98 为低 2%，1.1 为高 10%。范围 0.1～3；上架费另扣，净收益不为正则跳过。每批开始时锁定基准和系数。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.1f, 3f), Array.Empty<object>()));
		DiagnosticMode = ((BaseUnityPlugin)this).Config.Bind<bool>("99 诊断", "详细日志", false, ChineseSettings.ToggleDescription("记录界面与批处理诊断信息。"));
		ShowStatus = ((BaseUnityPlugin)this).Config.Bind<bool>("03 界面", "显示操作状态", true, ChineseSettings.ToggleDescription("仅显示已选数量和进行中的批处理进度。全部成功不弹提示；部分失败只通过游戏通知提醒一次。"));
		HoverPriceEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "悬浮显示价格", true, ChineseSettings.ToggleDescription("显示跳蚤平均参考价，未扣税；枪械包含已安装配件并单独列出配件合计。首次报价依赖服务端响应，相同型号短时复用缓存。不会改变仓库物品底色。"));
		HoverDelay = ((BaseUnityPlugin)this).Config.Bind<float>("04 价格显示", "悬浮显示延迟（秒）", 0.1f, new ConfigDescription("鼠标停留后显示物品价格提示框的等待时间。0 为立即显示，范围 0～2 秒；首次网络报价仍需等待服务端返回，缓存命中则立即填入价格。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
		HoverFleaOffers = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "跳蚤商品列表显示参考价", false, ChineseSettings.ToggleDescription("默认关闭，跳蚤商品列表保留游戏原提示；开启后附加跳蚤平均参考价。不会影响自己仓库中的价格显示。商人货架始终保留原生单件售价或兑换信息；所有储物容器内部有物品时只保留名称。"));
		HoverPriceUnit = ((BaseUnityPlugin)this).Config.Bind<PriceDisplayUnit>("04 价格显示", "金额显示单位", PriceDisplayUnit.千位k, "默认千位k：不足1000显示原数值，1000及以上始终用k，例如2114k；自动k或m：百万以上用m；完整金额：不缩写。只影响悬浮文字，不影响价格计算和出售金额。");
		HoverNameColor = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "提示框物品名称着色", true, ChineseSettings.ToggleDescription("仅对悬浮提示框里的物品名称按每格价值着色；不改变仓库名称、底色或枪械自定义方案名。"));
		HoverPriceColor = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "提示框价格着色", true, ChineseSettings.ToggleDescription("按旧版灰、白、绿、蓝、紫、橙价值等级显示比较价格；总计及配件合计保持普通文字颜色。"));
		HoverPerSlot = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "显示单格价格", true, ChineseSettings.ToggleDescription("单格价按单件价值除以占用格数；总计为整叠物品价值，与旧版价格模组一致。商人价格统一折算卢布。"));
		HoverTraderPrice = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "显示商人价格", false, ChineseSettings.ToggleDescription("控制自有物品的商人收购参考价，默认隐藏。不会隐藏商人货架本来的单件售价、货币或兑换信息。"));
		IncludeCustomTraderPrices = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "支持第三方商人估价", false, ChineseSettings.ToggleDescription("默认关闭，仅比较原版已解锁、可用商人的最高收购价。开启后把通过标准交易接口注册的第三方商人也纳入悬浮价格和对局收益估价；不是固定选一个商人。不支持无卢布汇率的自定义货币或独立交易系统。对局估价范围在开局锁定，局内切换从下一局生效，避免中途物价变动制造收益；悬浮价格即时生效。不改变快捷出售的商人选择。"));
		HoverEligibilityNote = ((BaseUnityPlugin)this).Config.Bind<bool>("04 价格显示", "显示上架条件提示", false, ChineseSettings.ToggleDescription("关闭后不在悬浮价格中显示等级、战局发现等条件提示；出售时仍会检查条件。"));
		((BaseUnityPlugin)this).Config.SettingChanged += delegate(object _, SettingChangedEventArgs args)
		{
			if (args.ChangedSetting.Definition.Section == "04 价格显示")
			{
				HoverPrices.Invalidate();
			}
		};
		Harmony val = new Harmony("local.shijiu.tools.spt4");
		ConfigEntry<bool> historyEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("08 对局记录", "启用对局记录", true, ChineseSettings.ToggleDescription("在自己及好友的个人档案加入对局记录。需要配套服务器模组；记录失败不影响其他功能。"));
		HistoryCapture.Enabled = historyEnabled.Value;
		historyEnabled.SettingChanged += delegate
		{
			HistoryCapture.Enabled = historyEnabled.Value;
		};
		Harmony val2 = new Harmony("local.shijiu.tools.spt4.history");
		try
		{
			HistorySettings.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[5]
			{
				typeof(HistoryOwnPatch),
				typeof(HistoryOtherPatch),
				typeof(HistoryOwnClosePatch),
				typeof(HistoryOtherClosePatch),
				typeof(HistoryNativeTabPatch)
			};
			foreach (Type type in array)
			{
				val2.CreateClassProcessor(type).Patch();
			}
			Logger.LogInfo((object)"对局记录客户端已加载：原生个人档案入口，配套服务端持久保存。");
		}
		catch (Exception ex)
		{
			try
			{
				val2.UnpatchSelf();
			}
			catch (Exception ex2)
			{
				Logger.LogError((object)ex2);
			}
			HistoryCapture.Enabled = false;
			Logger.LogError((object)("战绩模块未加载，其他功能保持运行：" + ex));
		}
		try
		{
			Type[] array = new Type[12]
			{
				typeof(ScreenShowPatch),
				typeof(ScreenClosePatch),
				typeof(ClickPatch),
				typeof(BeginDragPatch),
				typeof(DropPatch),
				typeof(TraderOpenPatch),
				typeof(TraderClosePatch),
				typeof(TraderModePatch),
				typeof(TraderClickPatch),
				typeof(TraderDropPatch),
				typeof(TraderBeginDragPatch),
				typeof(SelectionKillPatch)
			};
			foreach (Type type2 in array)
			{
				val.CreateClassProcessor(type2).Patch();
			}
		}
		catch
		{
			val.UnpatchSelf();
			throw;
		}
		((Component)this).gameObject.AddComponent<StashBehaviour>();
		Harmony val3 = new Harmony("local.shijiu.tools.spt4.quotes");
		try
		{
			Type[] array = new Type[3]
			{
				typeof(QuoteGameStopPatch),
				typeof(QuoteWorldDisposePatch),
				typeof(QuoteWorldStartPatch)
			};
			foreach (Type type3 in array)
			{
				val3.CreateClassProcessor(type3).Patch();
			}
		}
		catch (Exception ex3)
		{
			try
			{
				val3.UnpatchSelf();
			}
			catch (Exception ex4)
			{
				Logger.LogError((object)ex4);
			}
			Logger.LogError((object)("独立报价生命周期保护加载失败：" + ex3));
		}
		Harmony val4 = new Harmony("local.shijiu.tools.spt4.movement");
		try
		{
			MovementEnhancements.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[2]
			{
				typeof(EnhancedDropPatch),
				typeof(EnhancedPreviewPatch)
			};
			foreach (Type type4 in array)
			{
				val4.CreateClassProcessor(type4).Patch();
			}
			MovementEnhancements.Ready = true;
			Logger.LogInfo((object)"移动增强已加载：Ctrl空槽穿戴、原生事务交换。");
		}
		catch (Exception ex5)
		{
			MovementEnhancements.Ready = false;
			try
			{
				val4.UnpatchSelf();
			}
			catch (Exception ex6)
			{
				Logger.LogError((object)ex6);
			}
			Logger.LogError((object)("移动增强未加载，其他模块继续运行：" + ex5));
		}
		ChineseSettings.FindManager();
		Harmony val5 = new Harmony("local.shijiu.tools.spt4.modding");
		try
		{
			EquippedModding.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[5]
			{
				typeof(EquippedPlateAcceptPatch),
				typeof(EquippedPlateRemovePatch),
				typeof(EquippedModSlotPatch),
				typeof(EquippedPlateDropPatch),
				typeof(EquippedPlatePreviewPatch)
			};
			foreach (Type type5 in array)
			{
				val5.CreateClassProcessor(type5).Patch();
			}
			EquippedModding.Ready = true;
			Logger.LogInfo((object)"插板换装已加载：穿着护甲拆装及互换可拆插板，保留固定内衬与兼容检查。");
		}
		catch (Exception ex7)
		{
			EquippedModding.Ready = false;
			try
			{
				val5.UnpatchSelf();
			}
			catch (Exception ex8)
			{
				Logger.LogError((object)ex8);
			}
			Logger.LogError((object)("插板换装未加载，其他模块继续运行：" + ex7));
		}
		Logger.LogInfo((object)"【拾玖】多项功能优化 1.0.4 核心已加载：框选、快速出售、可选跳蚤定价；非战局发现开关不安装底层补丁。");
		Harmony val6 = new Harmony("local.shijiu.tools.spt4.hover");
		try
		{
			Type[] array = new Type[3]
			{
				typeof(HoverItemScopePatch),
				typeof(HoverTooltipPatch),
				typeof(HoverTooltipClosePatch)
			};
			foreach (Type type6 in array)
			{
				val6.CreateClassProcessor(type6).Patch();
			}
			HoverPrices.Ready = true;
			Logger.LogInfo((object)"【拾玖】多项功能优化 1.0.4 悬浮价格模块已加载。");
		}
		catch (Exception ex9)
		{
			HoverPrices.Ready = false;
			try
			{
				val6.UnpatchSelf();
			}
			catch (Exception ex10)
			{
				Logger.LogError((object)("撤销悬浮价格补丁失败：" + ex10));
			}
			Logger.LogError((object)("悬浮价格模块加载失败，框选和快速出售仍可用：" + ex9));
		}
		Harmony val7 = new Harmony("local.shijiu.tools.spt4.raid");
		try
		{
			RaidPanel.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[3]
			{
				typeof(RaidStartPatch),
				typeof(RaidPlayerStartPatch),
				typeof(RaidEndPatch)
			};
			foreach (Type type7 in array)
			{
				val7.CreateClassProcessor(type7).Patch();
			}
			RaidEarnings.Ready = true;
			Logger.LogInfo((object)"【拾玖】多项功能优化 1.0.4 对局收益模块已加载，等待进入对局建立基线。");
		}
		catch (Exception ex11)
		{
			RaidEarnings.Ready = false;
			try
			{
				val7.UnpatchSelf();
			}
			catch (Exception ex12)
			{
				Logger.LogError((object)ex12);
			}
			Logger.LogError((object)("对局收益模块加载失败，原有功能仍可用：" + ex11));
		}
		try
		{
			LoadoutValue.Configure(((BaseUnityPlugin)this).Config);
		}
		catch (Exception ex13)
		{
			LoadoutValue.Ready = false;
			Logger.LogError((object)("战备模块初始化失败：" + ex13));
		}
		Harmony val8 = new Harmony("local.shijiu.tools.spt4.summary");
		try
		{
			RaidSummary.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[2]
			{
				typeof(RaidFinishPatch),
				typeof(RaidSummaryShowPatch)
			};
			foreach (Type type8 in array)
			{
				val8.CreateClassProcessor(type8).Patch();
			}
		}
		catch (Exception ex14)
		{
			RaidSummary.Ready = false;
			try
			{
				val8.UnpatchSelf();
			}
			catch (Exception ex15)
			{
				Logger.LogError((object)ex15);
			}
			Logger.LogError((object)("结算模块初始化失败，其他功能继续运行：" + ex14));
		}
		Harmony val9 = new Harmony("local.shijiu.tools.spt4.pause");
		try
		{
			RaidPause.Configure(((BaseUnityPlugin)this).Config);
			Type[] array = new Type[8]
			{
				typeof(PauseWorldTickPatch),
				typeof(PauseHealthPatch),
				typeof(PauseExpirationPatch),
				typeof(PauseTimerUiPatch),
				typeof(PauseCommandPatch),
				typeof(PauseAxesPatch),
				typeof(PauseWorldEndPatch),
				typeof(PauseGameEndPatch)
			};
			foreach (Type type9 in array)
			{
				val9.CreateClassProcessor(type9).Patch();
			}
			((Component)this).gameObject.AddComponent<RaidPauseBehaviour>();
			RaidPause.Ready = true;
			Logger.LogInfo((object)"战局暂停已加载：统一热键，单机计时补偿；联机须接入匹配运行时的暂停适配器。");
		}
		catch (Exception ex16)
		{
			RaidPause.Ready = false;
			RaidPause.Dispose();
			try
			{
				val9.UnpatchSelf();
			}
			catch (Exception ex17)
			{
				Logger.LogError((object)ex17);
			}
			Logger.LogError((object)("暂停模块未加载，其他功能继续运行：" + ex16));
		}
	}
}
