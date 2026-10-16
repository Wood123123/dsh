using EFT.UI;
using HarmonyLib;
using NekoParty.GroupEX.UI;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MenuScreen), "Awake")]
public static class NekoPartyMenuScreenAwakePatch
{
	private static void Postfix(MenuScreen __instance)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		if ((Object)((Component)__instance).gameObject.GetComponent<NekoPartyMainMenuController>() == (Object)null)
		{
			((Component)__instance).gameObject.AddComponent<NekoPartyMainMenuController>();
		}
	}
}
