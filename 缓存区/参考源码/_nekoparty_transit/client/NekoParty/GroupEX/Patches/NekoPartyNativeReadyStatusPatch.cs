using System;
using EFT;
using EFT.UI.Matchmaker;
using Fika.Core.Main.Utils;

namespace NekoParty.GroupEX.Patches;

internal static class NekoPartyNativeReadyStatusPatch
{
	internal static bool Prefix(MatchmakerPlayersController __instance, bool isReady, Action callback)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite)
		{
			return true;
		}
		try
		{
			if (isReady)
			{
				((BaseMatchmakerController<RaidSettings>)(object)__instance).GetGroupStatus();
			}
		}
		catch (Exception ex)
		{
			FikaGlobals.LogError("[NekoParty] 就绪前刷新队伍状态失败：" + ex.Message, "NativeReadyStatus");
		}
		return true;
	}
}
