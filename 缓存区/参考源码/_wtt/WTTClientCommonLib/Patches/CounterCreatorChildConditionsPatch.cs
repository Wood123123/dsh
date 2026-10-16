using System;
using System.Reflection;
using System.Runtime.Serialization;
using Diz.Binding;
using EFT.Quests;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class CounterCreatorChildConditionsPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(ConditionCounterCreator), "OnDeserializedMethod", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(ConditionCounterCreator __instance, StreamingContext context)
	{
		ConditionCollection conditions = __instance.Conditions;
		if (conditions == null || ((BindableList<Condition>)(object)conditions).Count == 0)
		{
			return;
		}
		UpdatableBindableList<Condition> childConditions = ((Condition)__instance).ChildConditions;
		if (childConditions == null)
		{
			return;
		}
		foreach (Condition item in (BindableList<Condition>)(object)conditions)
		{
			if (item != null && (item is ConditionSalvage || item is ConditionLeaveItemAtLocation) && !((BindableList<Condition>)(object)childConditions).Contains(item))
			{
				((BindableList<Condition>)(object)childConditions).Add(item);
			}
		}
	}
}
