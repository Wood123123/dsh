using System;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(ModSlotView), "ModLock")]
internal static class EquippedModSlotPatch
{
	private static void Prefix(ModSlotView __instance, Slot slot, ref bool locked)
	{
		try
		{
			if (EquippedModding.PlateSlot(slot, InventoryAccess.Controller((SlotView)(object)__instance)?.TryCast<InventoryController>()))
			{
				locked = false;
			}
		}
		catch (Exception ex)
		{
			EquippedModding.Failed(ex);
		}
	}
}
