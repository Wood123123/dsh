using System.Linq;
using System.Reflection;
using EFT;
using EFT.UI;
using EFT.UI.Matchmaker;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch]
public static class NekoPartyAutoAdvanceInviteRaidSettingsPatch
{
	private static MethodBase TargetMethod()
	{
		return typeof(MatchmakerOfflineRaidScreen).GetMethods().FirstOrDefault((MethodInfo x) => x.Name == "Show" && x.GetParameters().Length == 3);
	}

	private static void Postfix(MatchmakerOfflineRaidScreen __instance, RaidSettings ___raidSettings_0, DefaultUIButton ____readyButton)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		if (NekoPartyMixedSideRuntime.ShouldAutoAdvanceInviteFlow(___raidSettings_0))
		{
			NekoPartyMixedSideRuntime.AutoAdvanceReadyButton((MonoBehaviour)__instance, ____readyButton);
		}
	}
}
