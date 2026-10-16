using System;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(OtherPlayerProfileScreen), "Close")]
internal static class HistoryOtherClosePatch
{
	private static void Prefix(OtherPlayerProfileScreen __instance)
	{
		try
		{
			HistoryUi.Close((Component?)(object)__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)ex.Message);
		}
	}
}
