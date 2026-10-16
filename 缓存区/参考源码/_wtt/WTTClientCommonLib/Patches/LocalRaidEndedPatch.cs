using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using Diz.Binding;
using EFT;
using EFT.Quests;
using HarmonyLib;
using JsonType;
using SPT.Reflection.Patching;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Patches;

internal class LocalRaidEndedPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(EftClientBackendSession), "LocalRaidEnded", new Type[4]
		{
			typeof(LocalRaidSettings),
			typeof(SessionResult),
			typeof(FlatItem[]),
			typeof(Dictionary<string, FlatItem[]>)
		}, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(LocalRaidSettings settings, SessionResult results, FlatItem[] lostInsuredItems, Dictionary<string, FlatItem[]> transferItems)
	{
		Player obj = Singleton<GameWorld>.Instance?.MainPlayer;
		QuestController obj2 = ((obj != null) ? obj.QuestController : null);
		QuestBook val = ((obj2 != null) ? obj2.Quests : null);
		if (val == null)
		{
			NestedQuestZonesCounterState.ClearAll();
			return;
		}
		foreach (Quest item in (BindableList<Quest>)(object)val)
		{
			NestedQuestZonesCounterState.ResetForQuest(item);
		}
	}
}
