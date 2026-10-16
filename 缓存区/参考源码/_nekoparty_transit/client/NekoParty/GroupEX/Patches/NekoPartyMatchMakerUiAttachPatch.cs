using Fika.Core.UI.Custom;
using HarmonyLib;
using NekoParty.GroupEX.UI;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerUIScript), "CreateMatchMakerUI")]
public static class NekoPartyMatchMakerUiAttachPatch
{
	private static void Postfix(MatchMakerUIScript __instance)
	{
		((Component)__instance).GetComponent<NekoPartyMatchMakerController>()?.EnsurePartyPanelAttached();
	}
}
