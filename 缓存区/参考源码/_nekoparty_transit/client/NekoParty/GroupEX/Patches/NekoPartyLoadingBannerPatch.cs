using EFT.UI.Matchmaker;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchmakerBannersPanel), "SelectBanner")]
public static class NekoPartyLoadingBannerPatch
{
	private static void Postfix(MatchmakerBannersPanel __instance, bool visible)
	{
		if (visible)
		{
			NekoPartyLoadingUiRuntime.ApplyBannerTip(__instance);
		}
	}
}
