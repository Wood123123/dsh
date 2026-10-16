using System.Collections.Generic;
using System.Linq;
using EFT;
using EFT.Quests;

namespace WTTClientCommonLib.Components;

public static class NestedQuestZonesCounterState
{
	public class State
	{
		public bool SalvageDone;

		public bool LeaveDone;
	}

	private static readonly Dictionary<(string questId, string ccId), State> _states = new Dictionary<(string, string), State>();

	public static State GetOrCreate(Quest quest, ConditionCounterCreator cc)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		(string, MongoID) tuple = (((Conditional<Quest>)(object)quest).Id, ((Condition)cc).id);
		Dictionary<(string questId, string ccId), State> states = _states;
		(string, MongoID) tuple2 = tuple;
		if (!states.TryGetValue((tuple2.Item1, MongoID.op_Implicit(tuple2.Item2)), out State value))
		{
			value = new State();
			Dictionary<(string questId, string ccId), State> states2 = _states;
			tuple2 = tuple;
			states2[(tuple2.Item1, MongoID.op_Implicit(tuple2.Item2))] = value;
		}
		return value;
	}

	public static void ResetForQuest(Quest quest)
	{
		foreach (var item in _states.Keys.Where<(string, string)>(((string questId, string ccId) k) => k.questId == ((Conditional<Quest>)(object)quest).Id).ToList())
		{
			_states.Remove(item);
		}
	}

	public static void ClearAll()
	{
		_states.Clear();
	}
}
