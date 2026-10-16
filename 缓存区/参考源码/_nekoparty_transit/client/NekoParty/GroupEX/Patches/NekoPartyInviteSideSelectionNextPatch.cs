using EFT.UI.Matchmaker;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerSideSelectionScreen), "method_18")]
public static class NekoPartyInviteSideSelectionNextPatch
{
	private static bool Prefix(MatchMakerSideSelectionScreen __instance)
	{
		if (!NekoPartyMixedSideRuntime.HasPendingInviteSelection())
		{
			return true;
		}
		return !NekoPartyMixedSideRuntime.RouteInviteSideSelectionNext(__instance);
	}
}
