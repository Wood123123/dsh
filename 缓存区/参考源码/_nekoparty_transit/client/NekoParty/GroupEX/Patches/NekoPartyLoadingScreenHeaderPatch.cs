using EFT;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerUI), "Awake")]
public static class NekoPartyLoadingScreenHeaderPatch
{
	private static void Postfix(MatchMakerUI __instance)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		if ((Object)__instance.LoadingScreenHeaderText != (Object)null)
		{
			((TMP_Text)__instance.LoadingScreenHeaderText).SetText(LocalizationExtensions.Localized("NKP_LoadingScreenHeader", (string)null), true);
		}
		if ((Object)__instance.LoadingScreenInfoText != (Object)null)
		{
			((TMP_Text)__instance.LoadingScreenInfoText).enableWordWrapping = true;
			((TMP_Text)__instance.LoadingScreenInfoText).SetText(LocalizationExtensions.Localized("NKP_LoadingScreenDescription", (string)null), true);
		}
	}
}
