using System;
using EFT;
using EFT.UI.Matchmaker;
using HarmonyLib;
using NekoParty.GroupEX.MixedSide;
using TMPro;
using UnityEngine;

namespace NekoParty.GroupEX.Patches;

[HarmonyPatch(typeof(MatchMakerSideSelectionScreen), "method_12")]
public static class NekoPartyInviteSideSelectionPatch
{
	private static void Prefix(ESideType side)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		if (NekoPartyMixedSideRuntime.HasPendingInviteSelection())
		{
			NekoPartyMixedSideRuntime.ApplyInviteSelectedSide(side);
		}
	}

	private static void Postfix(MatchMakerSideSelectionScreen __instance)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		if (!NekoPartyMixedSideRuntime.HasPendingInviteSelection())
		{
			return;
		}
		TextMeshProUGUI value = Traverse.Create((object)__instance).Field("_sideDescriptionText").GetValue<TextMeshProUGUI>();
		if (!((Object)value == (Object)null))
		{
			string text = LocalizationExtensions.Localized("NKP_RaidInviteSideSelectionHint", (string)null);
			if (!((TMP_Text)value).text.Contains(text, StringComparison.Ordinal))
			{
				((TMP_Text)value).text = ((TMP_Text)value).text + text;
			}
		}
	}
}
