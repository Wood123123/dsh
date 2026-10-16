using System;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(Tab), "OnPointerClick")]
internal static class HistoryNativeTabPatch
{
	private static void Postfix(Tab __instance)
	{
		try
		{
			HistoryUi.NativeOverview(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)ex.Message);
		}
	}
}
