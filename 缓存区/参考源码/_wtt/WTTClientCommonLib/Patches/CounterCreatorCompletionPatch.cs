using System;
using System.Collections.Generic;
using System.Reflection;
using Diz.Binding;
using EFT;
using EFT.Quests;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class CounterCreatorCompletionPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Quest), "IsConditionDone", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(Quest __instance, Condition condition, ref bool __result)
	{
		if (__instance != null && condition != null && !(condition is ConditionCounterCreator) && (condition is ConditionSalvage || condition is ConditionLeaveItemAtLocation))
		{
			ConditionCounterCreator val = FindParentCounterCreator(__instance, condition);
			if (val != null && IsCounterCreatorComplete(__instance, val))
			{
				__result = true;
			}
		}
	}

	private static bool IsCounterCreatorComplete(Quest quest, ConditionCounterCreator cc)
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		if (quest == null || cc == null)
		{
			return false;
		}
		if (((Conditional<Quest>)(object)quest).CompletedConditions != null && ((Conditional<Quest>)(object)quest).CompletedConditions.Contains(((Condition)cc).id))
		{
			return true;
		}
		float num = ((Condition)cc).value;
		if (num <= 0f)
		{
			num = 1f;
		}
		ConditionCounterManager conditionCountersManager = ((Conditional<Quest>)(object)quest).ConditionCountersManager;
		TaskConditionCounter val = ((conditionCountersManager != null) ? conditionCountersManager.GetCounter(((Condition)cc).id) : null);
		if (val != null && (float)val.Value >= num)
		{
			return true;
		}
		if (((Conditional<Quest>)(object)quest).ProgressCheckers != null && ((Conditional<Quest>)(object)quest).ProgressCheckers.TryGetValue((Condition)(object)cc, out var value) && value != null && value.CurrentValue >= (double)num)
		{
			return true;
		}
		return false;
	}

	private static ConditionCounterCreator? FindParentCounterCreator(Quest quest, Condition child)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		if (quest == null || child == null || !child.ParentId.HasValue)
		{
			return null;
		}
		MongoID value = child.ParentId.Value;
		if (((Conditional<Quest>)(object)quest).Conditions == null)
		{
			return null;
		}
		foreach (KeyValuePair<EQuestStatus, ConditionCollection> item in (Dictionary<EQuestStatus, ConditionCollection>)(object)((Conditional<Quest>)(object)quest).Conditions)
		{
			ConditionCollection value2 = item.Value;
			if (value2 == null)
			{
				continue;
			}
			foreach (Condition item2 in (BindableList<Condition>)(object)value2)
			{
				ConditionCounterCreator val = (ConditionCounterCreator)(object)((item2 is ConditionCounterCreator) ? item2 : null);
				if (val != null && ((Condition)val).id == value)
				{
					return val;
				}
			}
		}
		return null;
	}
}
