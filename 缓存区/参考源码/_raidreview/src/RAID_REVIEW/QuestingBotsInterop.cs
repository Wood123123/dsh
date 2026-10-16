using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace RAID_REVIEW;

internal static class QuestingBotsInterop
{
	private static bool _QuestingBotsLoadedChecked;

	private static bool _QuestingBotsInteropInited;

	private static bool _IsQuestingBotsLoaded;

	private static Type _QuestingBotsExternalType;

	private static MethodInfo _GetCurrentDecisionMethod;

	private static MethodInfo _GetCurrentQuestActionTypeMethod;

	private static MethodInfo _GetCurrentQuestNameMethod;

	private static MethodInfo _GetCurrentQuestLocationMethod;

	private static MethodInfo _IsCurrentJobAssignmentAnEftQuestMethod;

	private static MethodInfo _IsCurrentJobAssignmentActiveMethod;

	private static MethodInfo _HasAQuestingBossMethod;

	private static MethodInfo _GetJobAssignmentHistoryCsvDataMethod;

	public static bool IsQuestingBotsLoaded()
	{
		if (!_QuestingBotsLoadedChecked)
		{
			_QuestingBotsLoadedChecked = true;
			_IsQuestingBotsLoaded = Chainloader.PluginInfos.ContainsKey("com.danw.questingbots");
		}
		return _IsQuestingBotsLoaded;
	}

	public static bool Init()
	{
		if (!IsQuestingBotsLoaded())
		{
			return false;
		}
		if (!_QuestingBotsInteropInited)
		{
			_QuestingBotsInteropInited = true;
			_QuestingBotsExternalType = Type.GetType("QuestingBots.QuestingBotsExternal, QuestingBots-Client");
			if (_QuestingBotsExternalType != null)
			{
				_GetCurrentDecisionMethod = AccessTools.Method(_QuestingBotsExternalType, "GetCurrentDecision", (Type[])null, (Type[])null);
				_GetCurrentQuestActionTypeMethod = AccessTools.Method(_QuestingBotsExternalType, "GetCurrentQuestActionType", (Type[])null, (Type[])null);
				_GetCurrentQuestNameMethod = AccessTools.Method(_QuestingBotsExternalType, "GetCurrentQuestName", (Type[])null, (Type[])null);
				_GetCurrentQuestLocationMethod = AccessTools.Method(_QuestingBotsExternalType, "GetCurrentQuestLocation", (Type[])null, (Type[])null);
				_IsCurrentJobAssignmentAnEftQuestMethod = AccessTools.Method(_QuestingBotsExternalType, "IsCurrentJobAssignmentAnEftQuest", (Type[])null, (Type[])null);
				_IsCurrentJobAssignmentActiveMethod = AccessTools.Method(_QuestingBotsExternalType, "HasActiveJobAssignment", (Type[])null, (Type[])null);
				_HasAQuestingBossMethod = AccessTools.Method(_QuestingBotsExternalType, "HasAQuestingBoss", (Type[])null, (Type[])null);
				_GetJobAssignmentHistoryCsvDataMethod = AccessTools.Method(_QuestingBotsExternalType, "GetJobAssignmentHistoryCsvData", (Type[])null, (Type[])null);
			}
		}
		return _QuestingBotsExternalType != null;
	}

	public static QuestingBotsBotQuestInfo GetBotQuestInfo(BotOwner bot)
	{
		//IL_013f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		if (!Init())
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_GetCurrentDecisionMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_GetCurrentQuestActionTypeMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_GetCurrentQuestNameMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_GetCurrentQuestLocationMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_IsCurrentJobAssignmentAnEftQuestMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_IsCurrentJobAssignmentActiveMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		if (_HasAQuestingBossMethod == null)
		{
			return new QuestingBotsBotQuestInfo();
		}
		string currentDecision = (string)_GetCurrentDecisionMethod.Invoke(null, new object[1] { bot });
		string currentActionType = (string)_GetCurrentQuestActionTypeMethod.Invoke(null, new object[1] { bot });
		bool flag = (bool)_IsCurrentJobAssignmentActiveMethod.Invoke(null, new object[1] { bot });
		bool flag2 = (bool)_HasAQuestingBossMethod.Invoke(null, new object[1] { bot });
		if (!flag | flag2)
		{
			return new QuestingBotsBotQuestInfo(currentDecision, currentActionType);
		}
		string questName = (string)_GetCurrentQuestNameMethod.Invoke(null, new object[1] { bot });
		Vector3 questLocation = (Vector3)_GetCurrentQuestLocationMethod.Invoke(null, new object[1] { bot });
		bool isEftQuest = (bool)_IsCurrentJobAssignmentAnEftQuestMethod.Invoke(null, new object[1] { bot });
		return new QuestingBotsBotQuestInfo(currentDecision, currentActionType, questName, questLocation, isEftQuest);
	}

	public static IEnumerable<QuestingBotsBotJobAssignmentHistoryEntry> GetJobAssignmentHistory(BotOwner bot)
	{
		if (!Init() || _GetJobAssignmentHistoryCsvDataMethod == null)
		{
			yield break;
		}
		IEnumerable<string[]> enumerable = (IEnumerable<string[]>)_GetJobAssignmentHistoryCsvDataMethod.Invoke(null, new object[1] { bot });
		foreach (string[] item in enumerable)
		{
			yield return new QuestingBotsBotJobAssignmentHistoryEntry(item[0], item[1], item[2], item[3], item[4], item[5]);
		}
	}
}
