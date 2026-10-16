using System;
using System.Reflection;
using EFT.Quests;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

public class CreateConditionPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ConditionProgressCheckersFactory), "Create", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool Prefix(Condition condition, ref ConditionProgressChecker __result)
	{
		if (condition is ConditionSalvage condition2)
		{
			__result = (ConditionProgressChecker)(object)new SalvageConditionChecker(condition2);
			return false;
		}
		ConditionLeaveItemAtLocation val = (ConditionLeaveItemAtLocation)(object)((condition is ConditionLeaveItemAtLocation) ? condition : null);
		if (val != null)
		{
			__result = (ConditionProgressChecker)(object)new LeaveItemAtLocationChecker(val);
			return false;
		}
		return true;
	}
}
