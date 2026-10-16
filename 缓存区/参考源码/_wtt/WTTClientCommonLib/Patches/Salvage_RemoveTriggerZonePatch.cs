using System;
using System.Reflection;
using EFT;
using EFT.Interactive;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class Salvage_RemoveTriggerZonePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Player), "RemoveTriggerZone", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(Player __instance, TriggerWithId zone)
	{
		if (zone is SalvageItemTrigger zone2)
		{
			SalvageZoneTracker.Clear(__instance, zone2);
			__instance.SearchForInteractions();
		}
	}
}
