using System;
using System.Reflection;
using CookingGrenades.Config;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace CookingGrenades.Patches;

public class MedicineWheelInputPatch : ModulePatch
{
	private static readonly Type TranslateResultType = AccessTools.TypeByName("ETranslateResult");

	private static readonly object BlockedResult = Enum.ToObject(TranslateResultType, 2);

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EftGamePlayerOwner), "TranslateCommand", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool PatchPrefix(EftGamePlayerOwner __instance, ECommand command, ref object __result)
	{
		MedicineWheel orCreateInstance = WheelBase<MedicineWheel>.GetOrCreateInstance<MedicineWheel>();
		if (!ConfigManager.EnableMedicineWheel.Value)
		{
			return true;
		}
		if (orCreateInstance.IsOpen)
		{
			__result = BlockedResult;
			return false;
		}
		return true;
	}
}
