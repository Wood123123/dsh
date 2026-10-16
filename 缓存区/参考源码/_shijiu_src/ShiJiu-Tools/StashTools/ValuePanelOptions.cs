using System;
using BepInEx.Configuration;

namespace StashTools;

internal sealed class ValuePanelOptions
{
	internal readonly ConfigEntry<bool> Enabled;

	internal readonly ConfigEntry<float> X;

	internal readonly ConfigEntry<float> Y;

	internal readonly ConfigEntry<float> TitleSize;

	internal readonly ConfigEntry<float> DetailSize;

	internal readonly ConfigEntry<string> TitleColor;

	internal readonly ConfigEntry<string> DetailColor;

	internal readonly ConfigEntry<string>? NegativeColor;

	internal readonly ConfigEntry<RaidAmountUnit> Unit;

	internal ValuePanelOptions(ConfigFile config, string group, string enabled, float x, float y, string origin, bool hasNet = false)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Expected O, but got Unknown
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Expected O, but got Unknown
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Expected O, but got Unknown
		base._002Ector();
		Enabled = config.Bind<bool>(group, enabled, true, ChineseSettings.ToggleDescription("显示两行估值文字。仅供参考，不代表实际出售收入；报价不全时会明确标注。"));
		X = config.Bind<float>(group, "水平位置X", x, new ConfigDescription(origin + "，正数向右。F12 调整立即生效。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(-2000f, 2000f), Array.Empty<object>()));
		Y = config.Bind<float>(group, "垂直位置Y", y, new ConfigDescription(origin + "，正数向下。F12 调整立即生效。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(-1200f, 1200f), Array.Empty<object>()));
		TitleSize = config.Bind<float>(group, "第一行字号", 26f, new ConfigDescription("第一行加粗。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(12f, 48f), Array.Empty<object>()));
		DetailSize = config.Bind<float>(group, "第二行字号", 18f, new ConfigDescription("第二行普通字重。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(10f, 36f), Array.Empty<object>()));
		TitleColor = config.Bind<string>(group, "第一行颜色", "#E0E3D9", "颜色代码 #RRGGBB 或 #RRGGBBAA。");
		DetailColor = config.Bind<string>(group, "第二行颜色", "#B8BDB8", "颜色代码 #RRGGBB 或 #RRGGBBAA。");
		if (hasNet)
		{
			NegativeColor = config.Bind<string>(group, "负净收益颜色", "#E08275", "结算净收益为负时的颜色代码。");
		}
		Unit = config.Bind<RaidAmountUnit>(group, "金额显示单位", RaidAmountUnit.完整金额, "完整金额默认使用千位分隔符；也可选择固定 K 或 M。");
	}

	internal string Amount(double value)
	{
		return RaidLedger.A(value, Unit.Value);
	}
}
