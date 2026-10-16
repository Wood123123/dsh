using System;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerAcceptScreen), "method_14", new Type[] { typeof(EMatchingType) })]
public static class NekoPartyAcceptScreenMethod14Patch
{
	private static void Postfix(MatchMakerAcceptScreen __instance, EMatchingType matchingType)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		if (NekoGroupExConfig.UseNativePartyInvite)
		{
			NekoPartyPreparePreview.TryRender(__instance, matchingType);
		}
	}
}
