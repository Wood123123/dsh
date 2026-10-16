using System;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(OverallScreen), "Close")]
internal static class HistoryOwnClosePatch
{
	private static void Prefix(OverallScreen __instance)
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
