using EFT.UI.Matchmaker;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerAcceptScreen), "method_7")]
public static class NekoPartyAcceptScreenMethod7Patch
{
	private static bool Prefix(MatchMakerAcceptScreen __instance)
	{
		return true;
	}
}
