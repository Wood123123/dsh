using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using EFT;
using EFT.UI;
using EFT.UI.Ragfair;

namespace StashTools;

internal static class FleaRules
{
	private static readonly FleaSettingOverride State = new FleaSettingOverride();

	private static bool _lastEnabled;

	private static bool _failed;

	private static long _nextTick;

	internal static void Tick(bool force = false)
	{
		bool flag = Plugin.Enabled.Value && Plugin.AllowNonFirFlea.Value;
		if (!flag && !State.Tracking)
		{
			_lastEnabled = false;
			_failed = false;
			return;
		}
		bool flag2 = flag != _lastEnabled;
		_lastEnabled = flag;
		if (flag2)
		{
			_failed = false;
		}
		if (_failed)
		{
			return;
		}
		long num = Stopwatch.GetTimestamp() / (Stopwatch.Frequency / 1000);
		if (!force && !flag2 && num < _nextTick)
		{
			return;
		}
		_nextTick = num + 250;
		try
		{
			switch (State.Update(flag, Resolve))
			{
			case FleaSettingOverride.Change.Applied:
				Plugin.Logger.LogInfo((object)"非战局发现上架已开启：临时放宽游戏上架规则，未修改物品带勾状态。");
				break;
			case FleaSettingOverride.Change.AlreadyAllowed:
				Plugin.Logger.LogInfo((object)"非战局发现上架已开启：服务器原始规则已允许。");
				break;
			case FleaSettingOverride.Change.Restored:
			case FleaSettingOverride.Change.Released:
				Plugin.Logger.LogInfo((object)"非战局发现上架覆盖已关闭：保留服务器原始规则或后续外部修改。");
				break;
			case FleaSettingOverride.Change.ExternalChange:
				Plugin.Logger.LogWarning((object)"跳蚤规则被外部重新设置，本模组停止覆盖；如需重新开启，请关闭再打开此开关。");
				break;
			}
		}
		catch (Exception ex)
		{
			_failed = true;
			Plugin.Logger.LogError((object)("非战局发现上架设置应用失败，已停止重试；快捷出售继续遵循游戏现有规则：" + ex));
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static FleaSettingOverride.Binding? Resolve()
	{
		ItemUiContext? uI = StashBehaviour.UI;
		IEftSession val = ((uI != null) ? uI.Session : null);
		if (val == null || val.RagFair == null)
		{
			return null;
		}
		RagfairSettings settings = RagFair.Settings;
		if (settings == null)
		{
			return null;
		}
		return new FleaSettingOverride.Binding((Session: val.Identity().ToInt64(), Settings: settings.Identity().ToInt64()), settings.isOnlyFoundInRaidAllowed, delegate(bool value)
		{
			settings.isOnlyFoundInRaidAllowed = value;
		});
	}
}
