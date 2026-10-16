using System;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace StashTools;

[HarmonyPatch(typeof(SimpleTooltip), "Show")]
internal static class HoverTooltipPatch
{
	private static void Prefix(SimpleTooltip __instance, ref string text, ref float delay)
	{
		try
		{
			if ((Object)(object)HoverPrices.Scope != (Object)null && Plugin.HoverPriceEnabled.Value)
			{
				delay = Plugin.HoverDelay.Value;
			}
			HoverPrices.Attach(__instance, ref text);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogWarning((object)("悬浮提示扩展失败：" + ex.Message));
		}
	}

	private static void Postfix(SimpleTooltip __instance)
	{
		try
		{
			HoverPrices.Resize(__instance);
		}
		catch (Exception ex)
		{
			CompactTooltip.Close();
			Plugin.Logger.LogWarning((object)("悬浮提示尺寸调整失败：" + ex.Message));
		}
	}
}
