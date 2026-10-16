using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class Salvage_AddTriggerZonePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), "AddTriggerZone", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(Player __instance, TriggerWithId zone)
	{
		if (zone is SalvageItemTrigger zone2)
		{
			SalvageZoneTracker.Set(__instance, zone2);
			__instance.SearchForInteractions();
		}
	}
}
