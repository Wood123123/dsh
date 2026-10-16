using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.Binding;
using EFT;
using EFT.Quests;
using HarmonyLib;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

internal class ItemDroppedAtPlacePatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(Profile), "ItemDroppedAtPlace", (Type[])null, (Type[])null);
	}

	[PatchPrefix]
	public static bool Prefix(Profile __instance, string itemId, string zoneId)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Expected O, but got Unknown
		//IL_02ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_028d: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		Player val = Singleton<GameWorld>.Instance?.MainPlayer;
		QuestController questController = val.QuestController;
		QuestBook val2 = ((questController != null) ? questController.Quests : null);
		if (val2 == null)
		{
			return true;
		}
		foreach (Quest item in (BindableList<Quest>)(object)val2)
		{
			if (((Conditional<Quest>)(object)item).ProgressCheckers == null || ((Conditional<Quest>)(object)item).Conditions == null)
			{
				continue;
			}
			foreach (KeyValuePair<EQuestStatus, ConditionCollection> item2 in (Dictionary<EQuestStatus, ConditionCollection>)(object)((Conditional<Quest>)(object)item).Conditions)
			{
				EQuestStatus key = item2.Key;
				ConditionCollection value = item2.Value;
				if (value == null)
				{
					continue;
				}
				foreach (Condition item3 in (BindableList<Condition>)(object)value)
				{
					ConditionCounterCreator val3 = (ConditionCounterCreator)(object)((item3 is ConditionCounterCreator) ? item3 : null);
					if (val3 == null || val3.Conditions == null || !((IEnumerable<Condition>)val3.Conditions).Any(delegate(Condition cond)
					{
						ConditionZone val7 = (ConditionZone)(object)((cond is ConditionZone) ? cond : null);
						return val7 != null && (val7 is ConditionSalvage || val7 is ConditionLeaveItemAtLocation);
					}))
					{
						continue;
					}
					ConditionProgressChecker val4 = null;
					ConditionSalvage conditionSalvage = null;
					ConditionLeaveItemAtLocation val5 = null;
					SalvageConditionChecker salvageConditionChecker = null;
					LeaveItemAtLocationChecker leaveItemAtLocationChecker = null;
					val4 = (((Conditional<Quest>)(object)item).ProgressCheckers.TryGetValue((Condition)(object)val3, out var value2) ? value2 : null);
					foreach (Condition item4 in (BindableList<Condition>)(object)val3.Conditions)
					{
						ConditionZone val6 = (ConditionZone)(object)((item4 is ConditionZone) ? item4 : null);
						if (val6 != null && !(val6.zoneId != zoneId))
						{
							ConditionProgressChecker value4;
							if (item4 is ConditionSalvage && ((Conditional<Quest>)(object)item).ProgressCheckers.TryGetValue(item4, out var value3))
							{
								conditionSalvage = (ConditionSalvage)(object)item4;
								salvageConditionChecker = (SalvageConditionChecker)(object)value3;
							}
							else if (item4 is ConditionLeaveItemAtLocation && ((Conditional<Quest>)(object)item).ProgressCheckers.TryGetValue(item4, out value4))
							{
								val5 = (ConditionLeaveItemAtLocation)item4;
								leaveItemAtLocationChecker = (LeaveItemAtLocationChecker)(object)value4;
							}
						}
					}
					if (conditionSalvage == null && val5 == null)
					{
						continue;
					}
					ConditionCounterManager conditionCountersManager = ((Conditional<Quest>)(object)item).ConditionCountersManager;
					TaskConditionCounter ccCounter = null;
					if (conditionCountersManager != null)
					{
						ccCounter = conditionCountersManager.GetCounter(((Condition)val3).id);
					}
					NestedQuestZonesCounterState.State state = NestedQuestZonesCounterState.GetOrCreate(item, val3);
					if (val4 != null && !val4.HasGetter())
					{
						float target = ((Condition)val3).value;
						if (target <= 0f)
						{
							target = 1f;
						}
						val4.SetCurrentValueGetter((Func<ConditionProgressChecker, double>)((ConditionProgressChecker _) => (!state.SalvageDone || !state.LeaveDone) ? 0.0 : ((double)target)));
					}
					if (conditionSalvage != null && salvageConditionChecker != null)
					{
						if (state.SalvageDone)
						{
							return false;
						}
						float value5 = ((Condition)conditionSalvage).value;
						if (((ConditionProgressChecker)salvageConditionChecker).Test((object)value5))
						{
							state.SalvageDone = true;
							((ConditionProgressChecker)salvageConditionChecker)._currentValue = true;
							SignalConditionCompleted(val, item, key, (Condition)(object)conditionSalvage);
						}
						return false;
					}
					if (val5 != null && leaveItemAtLocationChecker != null)
					{
						float value6 = ((Condition)val5).value;
						if (((ConditionProgressChecker)leaveItemAtLocationChecker).Test((object)value6))
						{
							state.LeaveDone = true;
							SignalConditionCompleted(val, item, key, (Condition)(object)val5);
						}
						if (TryCompleteCounterCreator(val3, ccCounter, state, val4))
						{
							SignalConditionCompleted(val, item, key, (Condition)(object)val3);
						}
						return false;
					}
				}
			}
		}
		return true;
	}

	private static void SignalConditionCompleted(Player player, Quest quest, EQuestStatus status, Condition condition)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		((ConditionalController<Quest>)(object)((player != null) ? player.QuestController : null))?.OnConditionValueChanged(quest, status, condition, true);
	}

	private static bool TryCompleteCounterCreator(ConditionCounterCreator cc, TaskConditionCounter ccCounter, NestedQuestZonesCounterState.State state, ConditionProgressChecker ccCpc)
	{
		if (!state.SalvageDone || !state.LeaveDone)
		{
			return false;
		}
		if (ccCpc == null)
		{
			return false;
		}
		float num = ((Condition)cc).value;
		if (num <= 0f)
		{
			num = 1f;
		}
		if (ccCounter != null && (float)ccCounter.Value < num)
		{
			ccCounter.Value = (int)num;
		}
		return ccCpc.Test((object)num);
	}
}
