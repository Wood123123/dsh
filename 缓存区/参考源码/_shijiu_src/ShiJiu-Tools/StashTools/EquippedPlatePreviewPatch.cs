using System;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(SlotView), "HighlightItemViewPosition")]
internal static class EquippedPlatePreviewPatch
{
	private static bool Prefix(SlotView __instance, DragItemContext itemContext)
	{
		try
		{
			return !PlateExchange.Preview(__instance, itemContext);
		}
		catch (Exception ex)
		{
			EquippedModding.Failed(ex);
			return true;
		}
	}
}
