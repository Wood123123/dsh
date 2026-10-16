using System;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace WTTClientCommonLib.Patches;

public class FixCustomItemSortingOrderPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ItemExtensions), "GetIndexOfItemType", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool PatchPrefix(ref int __result, Item i)
	{
		Type type = ((object)i).GetType();
		int num = ItemSorter.IndexOf(type);
		if (num >= 0)
		{
			__result = num;
			return false;
		}
		Type type2 = type;
		while (type2 != null)
		{
			num = ItemSorter.IndexOf(type2);
			if (num >= 0)
			{
				__result = num;
				return false;
			}
			type2 = type2.BaseType;
		}
		__result = int.MaxValue;
		return false;
	}
}
