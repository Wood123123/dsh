using System;
using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(Tooltip), "Close")]
internal static class HoverTooltipClosePatch
{
	private static void Prefix(Tooltip __instance)
	{
		try
		{
			HoverPrices.Close(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("悬浮提示关闭失败：" + ex.Message));
		}
	}
}
