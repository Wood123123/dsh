using System;
using EFT.InventoryLogic;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(CantRemoveFromSlotsDuringRaidComponent), "CanRemoveFromSlotDuringRaid")]
internal static class EquippedPlateRemovePatch
{
	private static void Postfix(CantRemoveFromSlotsDuringRaidComponent __instance, ref bool __result)
	{
		try
		{
			if (!__result)
			{
				Item item = ((ItemComponent)__instance).Item;
				if (item?.TryCast<ArmorPlate>() != null && EquippedModding.PlateSlot(item.CurrentAddress?.TryCast<SlotItemAddress>()?.Slot))
				{
					__result = true;
				}
			}
		}
		catch (Exception ex)
		{
			EquippedModding.Failed(ex);
		}
	}
}
