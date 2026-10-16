using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CookingGrenades.Config;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class GrenadeSelectorPatch : ModulePatch
{
	private static readonly Type GrenadeSelectorType = AccessTools.AllTypes().FirstOrDefault((Type t) => t.Name == "GrenadeSelector");

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(GrenadeSelectorType, "ShowGrenades", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool PatchPrefix(ref object __result)
	{
		if (ConfigManager.EnableGrenadeWheel.Value)
		{
			__result = Task.FromResult<ThrowWeap>(null);
			return false;
		}
		return true;
	}
}
