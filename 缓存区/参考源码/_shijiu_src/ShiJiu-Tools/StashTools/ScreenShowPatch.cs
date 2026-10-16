using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class ScreenShowPatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return from m in typeof(InventoryScreen).GetMethods()
			where m.Name == "Show" && m.DeclaringType == typeof(InventoryScreen)
			select m;
	}

	private static void Postfix(InventoryScreen __instance)
	{
		StashBehaviour.Open(__instance);
	}
}
