using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace StashTools;

[HarmonyPatch(typeof(ItemView), "OnEndDrag")]
internal static class EnhancedDropPatch
{
	private static bool Prefix(ItemView __instance, PointerEventData eventData)
	{
		try
		{
			return !MovementEnhancements.TryDrop(__instance, eventData);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError((object)("移动增强结束拖动异常：" + ex));
			return true;
		}
	}

	private static void Postfix()
	{
		MovementEnhancements.End();
	}
}
