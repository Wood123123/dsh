using EFT.UI.Matchmaker;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchmakerPlayersController), "UpdateMatchingStatus")]
public static class NekoPartyLoadingStatusPatch
{
	private static void Prefix(ref string status)
	{
		status = NekoPartyLoadingUiRuntime.RemapStatus(status);
	}
}
