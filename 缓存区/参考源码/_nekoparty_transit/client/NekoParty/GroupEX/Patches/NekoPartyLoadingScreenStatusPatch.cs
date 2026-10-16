using EFT.UI.Matchmaker;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchmakerTimeHasCome), "ChangeStatus")]
public static class NekoPartyLoadingScreenStatusPatch
{
	private static void Prefix(ref string status)
	{
		status = NekoPartyLoadingUiRuntime.RemapStatus(status);
	}
}
