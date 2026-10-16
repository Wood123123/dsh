using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace CookingGrenades.Patches;

public class GamePlayerOwnerTranslateAxesPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GamePlayerOwner), "TranslateAxes", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool PatchPrefix(ref float[] axes)
	{
		if ((!((Object)(object)WheelBase<GrenadeWheel>.Instance != (Object)null) || !WheelBase<GrenadeWheel>.Instance.IsOpen) && (!((Object)(object)WheelBase<MedicineWheel>.Instance != (Object)null) || !WheelBase<MedicineWheel>.Instance.IsOpen))
		{
			return true;
		}
		if (axes != null)
		{
			for (int i = 2; i < axes.Length; i++)
			{
				axes[i] = 0f;
			}
		}
		return true;
	}
}
