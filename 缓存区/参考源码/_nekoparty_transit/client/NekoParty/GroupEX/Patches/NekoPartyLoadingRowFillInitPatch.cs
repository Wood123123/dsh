using HarmonyLib;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(LoadingScreenPlayer), "SetNickname")]
public static class NekoPartyLoadingRowFillInitPatch
{
	private static void Postfix(LoadingScreenPlayer __instance)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Expected O, but got Unknown
		if (!((Object)__instance == (Object)null) && !((Object)__instance.ProgressBar == (Object)null))
		{
			(((Component)__instance).GetComponent<NekoRowFillColorKeeper>() ?? ((Component)__instance).gameObject.AddComponent<NekoRowFillColorKeeper>()).Target = __instance.ProgressBar;
		}
	}
}
