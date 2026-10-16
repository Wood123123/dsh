using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace StashTools;

[HarmonyPatch]
internal static class HoverItemScopePatch
{
	private static IEnumerable<MethodBase> TargetMethods()
	{
		return from m in new Type[4]
			{
				typeof(GridItemView),
				typeof(TradingItemView),
				typeof(TradingPlayerItemView),
				typeof(TransferItemView)
			}.SelectMany((Type t) => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			where m.Name == "ShowTooltip" && m.GetParameters().Length == 0 && !m.IsAbstract
			select m;
	}

	private static void Prefix(ItemView __instance, out ItemView? __state)
	{
		__state = HoverPrices.Scope;
		HoverPrices.Scope = __instance;
	}

	private static void Finalizer(ItemView? __state)
	{
		HoverPrices.Scope = __state;
	}
}
