using System.Reflection;
using EFT.Quests;
using EFT.UI;
using SPT.Reflection.Patching;
using UnityEngine;

namespace WTTClientCommonLib.Patches;

public class HideSecretLockedQuestsPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(QuestsListView).GetMethod("UpdateSingleQuestVisibility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
	}

	[PatchPostfix]
	private static void Postfix(QuestsListView __instance, QuestListItem questView)
	{
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Invalid comparison between Unknown and I4
		if (!((Object)(object)questView == (Object)null) && questView.Quest != null)
		{
			Quest quest = questView.Quest;
			if (quest.Template.ServerOnly && (int)((Conditional<Quest>)(object)quest).QuestStatus == 0)
			{
				((Component)questView).gameObject.SetActive(false);
			}
		}
	}
}
