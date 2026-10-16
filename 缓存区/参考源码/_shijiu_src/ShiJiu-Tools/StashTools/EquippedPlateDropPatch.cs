using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace StashTools;

[HarmonyPatch(typeof(ItemView), "OnEndDrag")]
internal static class EquippedPlateDropPatch
{
	[HarmonyPriority(800)]
	private static bool Prefix(ItemView __instance, PointerEventData eventData)
	{
		try
		{
			return !PlateExchange.Drop(__instance, eventData);
		}
		catch (Exception ex)
		{
			EquippedModding.Failed(ex);
			return true;
		}
	}
}
