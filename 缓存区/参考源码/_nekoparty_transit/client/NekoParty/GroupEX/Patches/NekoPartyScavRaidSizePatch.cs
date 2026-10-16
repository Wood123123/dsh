using System;
using EFT;
using EFT.Communications;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyScavRaidSizePatch
{
	internal static bool Prefix(MainMenuShowOperation __instance, ref bool __result)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		try
		{
			MatchmakerPlayersController matchmakerPlayersController = __instance.MatchmakerPlayersController;
			RaidSettings raidSettings_ = __instance.raidSettings_0;
			if (matchmakerPlayersController == null || raidSettings_ == null)
			{
				return true;
			}
			if ((int)raidSettings_.Side != 1)
			{
				return true;
			}
			if (((BaseMatchmakerController<RaidSettings>)(object)matchmakerPlayersController).GroupPlayers.Count <= 4)
			{
				return true;
			}
			string arg = null;
			double value = 0.0;
			if (!((BaseMatchmakerController<RaidSettings>)(object)matchmakerPlayersController).HasLockedScav(ref arg, ref value))
			{
				__result = true;
				return false;
			}
			string arg2 = TimeSpan.FromSeconds(value).ToString("hh\\:mm\\:ss");
			NotificationManager.DisplayWarningNotification(string.Format(LocalizationExtensions.Localized("ScavTimerNotification", (string)null), arg, arg2), (ENotificationDurationType)0);
			__result = false;
			return false;
		}
		catch (Exception arg3)
		{
			FikaGlobals.LogError($"[NekoParty] SCAV 大队上限解除失败，回退原版：{arg3}", "Prefix");
			return true;
		}
	}
}
