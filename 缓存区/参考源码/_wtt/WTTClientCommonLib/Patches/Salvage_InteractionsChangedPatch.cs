using System;
using System.Reflection;
using Diz.Binding;
using EFT;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class Salvage_InteractionsChangedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GamePlayerOwner), "InteractionsChangedHandler", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	private static bool Prefix(GamePlayerOwner __instance)
	{
		Player player = ((PlayerOwner)__instance).Player;
		if ((Object)(object)player == (Object)null)
		{
			return true;
		}
		if ((Object)(object)player.InteractableObject != (Object)null || (Object)(object)player.PlaceItemZone != (Object)null || (Object)(object)player.BtrInteractionSide != (Object)null || (Object)(object)player.TripwireInteractionTrigger != (Object)null || (Object)(object)player.EventObjectInteractive != (Object)null || (Object)(object)player.ExfiltrationPoint != (Object)null)
		{
			return true;
		}
		SalvageItemTrigger salvageItemTrigger = SalvageZoneTracker.Get(player);
		if ((Object)(object)salvageItemTrigger == (Object)null)
		{
			return true;
		}
		IInteractive val = (IInteractive)(object)salvageItemTrigger;
		AvailableInteractionState val2 = InteractionContextHelper.GetAvailableActions(__instance, val);
		ClientTransitController val3 = default(ClientTransitController);
		if (val2 == null && TransitController.Exist<ClientTransitController>(ref val3))
		{
			val2 = val3._availableInteractionState;
		}
		if (val2 != null)
		{
			val2.InitSelected();
		}
		((BaseBindable<AvailableInteractionState>)(object)__instance.AvailableInteractionState).Value = val2;
		return false;
	}
}
