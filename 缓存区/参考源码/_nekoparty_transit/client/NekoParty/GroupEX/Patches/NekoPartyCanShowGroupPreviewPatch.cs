using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.UI.Matchmaker;

namespace NekoParty.GroupEX.Patches;

public static class NekoPartyCanShowGroupPreviewPatch
{
	public static bool Prefix(MatchmakerPlayersController __instance, ref bool __result)
	{
		if (!NekoGroupExConfig.UseNativePartyInvite || __instance == null)
		{
			return true;
		}
		string selfAid = ((GroupPlayer)((BaseMatchmakerController<RaidSettings>)(object)__instance).CurrentPlayer).AccountId;
		if ((((IEnumerable<RaidPlayer>)((BaseMatchmakerController<RaidSettings>)(object)__instance).GroupPlayers)?.Count((RaidPlayer p) => p != null && ((GroupPlayer)p).AccountId != selfAid) ?? 0) > 0)
		{
			__result = true;
			return false;
		}
		return true;
	}
}
