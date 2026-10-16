using System;
using System.Reflection;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyAutoAdvanceInviteLocationPatch
{
	private static MethodBase TargetMethod()
	{
		return AccessTools.Method(typeof(MatchMakerSelectionLocationScreen), "Show", new Type[3]
		{
			typeof(IEftSession),
			typeof(RaidSettings),
			typeof(MatchmakerPlayersController)
		}, (Type[])null);
	}

	private static void Postfix(MatchMakerSelectionLocationScreen __instance, RaidSettings raidSettings, DefaultUIButton ____readyButton)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		if (NekoPartyMixedSideRuntime.ShouldAutoAdvanceInviteFlow(raidSettings))
		{
			NekoPartyMixedSideRuntime.AutoAdvanceReadyButton((MonoBehaviour)__instance, ____readyButton);
		}
	}
}
