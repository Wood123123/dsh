using System;
using System.Reflection;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.Patches;

internal class SalvageQuestIconPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(StaticIcons), "GetQuestIcon", (Type[])null, (Type[])null);
	}

	[PatchPostfix]
	public static void Postfix(StaticIcons __instance, Condition condition, ref Sprite __result)
	{
		if (!((Object)(object)__result != (Object)null) && condition != null && condition is ConditionSalvage)
		{
			Sprite salvageSprite = QuestIcons.SalvageSprite;
			if ((Object)(object)salvageSprite != (Object)null)
			{
				__result = salvageSprite;
			}
		}
	}
}
