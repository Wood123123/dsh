using System;
using EFT.InventoryLogic;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch(typeof(ArmorSlot), "CanAcceptRaid")]
internal static class EquippedPlateAcceptPatch
{
	private static void Postfix(ArmorSlot __instance, ref InventoryError error, ref bool __result)
	{
		try
		{
			if (!__result && EquippedModding.PlateSlot((Slot?)(object)__instance))
			{
				error = null;
				__result = true;
			}
		}
		catch (Exception ex)
		{
			EquippedModding.Failed(ex);
		}
	}
}
