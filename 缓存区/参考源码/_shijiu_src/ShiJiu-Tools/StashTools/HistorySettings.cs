using BepInEx.Configuration;

namespace StashTools;

internal static class HistorySettings
{
	internal static ConfigEntry<bool> ShowTeam;

	internal static void Configure(ConfigFile config)
	{
		ShowTeam = config.Bind<bool>("08 对局记录", "显示队伍成员统计", true, ChineseSettings.ToggleDescription("已关联的联机行动在详情顶部显示各成员收益、消耗、净收益、PMC和Scav/头目击杀及结果。点击已上传成员查看他的明细。没有联机适配器时保留个人记录，不猜测队伍关系。"));
		ShowTeam.SettingChanged += delegate
		{
			HistoryUi.SettingsChanged();
		};
	}
}
