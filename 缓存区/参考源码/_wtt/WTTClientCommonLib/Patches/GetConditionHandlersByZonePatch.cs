using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Diz.Binding;
using EFT.Quests;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class GetConditionHandlersByZonePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		Type[] array = new Type[1] { typeof(string) };
		Type[] array2 = new Type[1] { typeof(ConditionZone) };
		return AccessTools.Method(typeof(QuestBook), "GetConditionHandlersByZone", array, array2);
	}

	[PatchPostfix]
	public static void Postfix(QuestBook __instance, string zoneId, ref IEnumerable<ConditionProgressChecker> __result)
	{
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Invalid comparison between Unknown and I4
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Invalid comparison between Unknown and I4
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		List<ConditionProgressChecker> list = __result?.ToList() ?? new List<ConditionProgressChecker>();
		foreach (Quest item in (BindableList<Quest>)(object)__instance)
		{
			if (item == null || ((int)((Conditional<Quest>)(object)item).QuestStatus != 2 && (int)((Conditional<Quest>)(object)item).QuestStatus != 3) || ((Conditional<Quest>)(object)item).Conditions == null || ((Conditional<Quest>)(object)item).ProgressCheckers == null)
			{
				continue;
			}
			foreach (KeyValuePair<EQuestStatus, ConditionCollection> item2 in (Dictionary<EQuestStatus, ConditionCollection>)(object)((Conditional<Quest>)(object)item).Conditions)
			{
				EQuestStatus key = item2.Key;
				ConditionCollection value = item2.Value;
				if (value == null || (!Enumerable.Contains(((Conditional<Quest>)(object)item).CurrentStatusTransitions, key) && key != ((Conditional<Quest>)(object)item).QuestStatus))
				{
					continue;
				}
				foreach (Condition item3 in (BindableList<Condition>)(object)value)
				{
					ConditionCounterCreator val = (ConditionCounterCreator)(object)((item3 is ConditionCounterCreator) ? item3 : null);
					if (val == null || val.Conditions == null || IsCounterCreatorComplete(item, val))
					{
						continue;
					}
					foreach (Condition item4 in (BindableList<Condition>)(object)val.Conditions)
					{
						ConditionZone val2 = (ConditionZone)(object)((item4 is ConditionZone) ? item4 : null);
						if (val2 != null && !(val2.zoneId != zoneId) && (val2 is ConditionLeaveItemAtLocation || val2 is ConditionSalvage) && ((Conditional<Quest>)(object)item).ProgressCheckers.TryGetValue(item4, out var value2) && value2 != null && !list.Contains(value2))
						{
							list.Add(value2);
						}
					}
				}
			}
		}
		__result = list;
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
}
