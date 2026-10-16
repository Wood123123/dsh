using System;
using System.Reflection;
using EFT;
using UnityEngine;

namespace RAID_REVIEW;

internal class QuestingBots_Integration
{
	private static bool _reflectionInit;

	private static bool _available;

	private static MethodInfo _getCurrentJobAssignmentMethod;

	private static Type _botJobAssignmentType;

	private static PropertyInfo _jobStatusProp;

	private static PropertyInfo _jobPositionProp;

	private static PropertyInfo _jobQuestAssignmentProp;

	private static PropertyInfo _jobObjectiveStepProp;

	private static PropertyInfo _questNameProp;

	private static PropertyInfo _questIsEFTQuestProp;

	private static PropertyInfo _stepActionTypeProp;

	private static Type _questActionEnumType;

	public static void InitReflection()
	{
		if (_reflectionInit)
		{
			return;
		}
		_reflectionInit = true;
		if (QuestingBotsInterop.Init())
		{
			_available = true;
			LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: QUESTINGBOTS :::: Official interop detected (QB >= 0.11.0) — legacy reflection skipped");
			return;
		}
		try
		{
			Type type = Type.GetType("QuestingBots.Controllers.BotJobAssignmentFactory, QuestingBots-Client");
			if (type == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: QUESTINGBOTS :::: BotJobAssignmentFactory type not found");
				return;
			}
			_getCurrentJobAssignmentMethod = type.GetMethod("GetCurrentJobAssignment", BindingFlags.Static | BindingFlags.Public, null, new Type[2]
			{
				typeof(BotOwner),
				typeof(bool)
			}, null);
			if (_getCurrentJobAssignmentMethod == null)
			{
				_getCurrentJobAssignmentMethod = type.GetMethod("GetCurrentJobAssignment", BindingFlags.Static | BindingFlags.Public);
			}
			if (_getCurrentJobAssignmentMethod == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: QUESTINGBOTS :::: GetCurrentJobAssignment method not found");
				return;
			}
			_botJobAssignmentType = _getCurrentJobAssignmentMethod.ReturnType;
			_jobStatusProp = _botJobAssignmentType.GetProperty("Status");
			_jobPositionProp = _botJobAssignmentType.GetProperty("Position");
			_jobQuestAssignmentProp = _botJobAssignmentType.GetProperty("QuestAssignment");
			_jobObjectiveStepProp = _botJobAssignmentType.GetProperty("QuestObjectiveStepAssignment");
			if (_jobQuestAssignmentProp != null)
			{
				Type propertyType = _jobQuestAssignmentProp.PropertyType;
				_questNameProp = propertyType.GetProperty("Name");
				_questIsEFTQuestProp = propertyType.GetProperty("IsEFTQuest");
			}
			if (_jobObjectiveStepProp != null)
			{
				_stepActionTypeProp = _jobObjectiveStepProp.PropertyType.GetProperty("ActionType");
				if (_stepActionTypeProp != null)
				{
					_questActionEnumType = _stepActionTypeProp.PropertyType;
				}
			}
			_available = true;
			LoggerInstance.Log.LogInfo((object)("RAID_REVIEW :::: QUESTINGBOTS :::: Reflection init OK — " + $"Assignment={_botJobAssignmentType != null}, Status={_jobStatusProp != null}, " + $"Position={_jobPositionProp != null}, Quest={_jobQuestAssignmentProp != null}, " + $"QuestName={_questNameProp != null}, IsEFT={_questIsEFTQuestProp != null}, " + $"Step={_jobObjectiveStepProp != null}, ActionType={_stepActionTypeProp != null}"));
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogWarning((object)("RAID_REVIEW :::: QUESTINGBOTS :::: Reflection init failed: " + ex.Message));
		}
	}

	public static TrackingBotQuest GetBotQuestData(Player player, string sessionId, long time)
	{
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Unknown result type (might be due to invalid IL or missing references)
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0320: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Unknown result type (might be due to invalid IL or missing references)
		//IL_032b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0334: Unknown result type (might be due to invalid IL or missing references)
		//IL_0345: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0352: Unknown result type (might be due to invalid IL or missing references)
		if (!_available || (Object)(object)player == (Object)null || !player.IsAI)
		{
			return null;
		}
		try
		{
			IAIData aIData = player.AIData;
			BotOwner val = ((aIData != null) ? aIData.BotOwner : null);
			if ((Object)(object)val == (Object)null)
			{
				return null;
			}
			if (QuestingBotsInterop.Init())
			{
				QuestingBotsBotQuestInfo botQuestInfo = QuestingBotsInterop.GetBotQuestInfo(val);
				if (botQuestInfo == null || !botQuestInfo.IsValid)
				{
					return null;
				}
				if (!botQuestInfo.HasAQuest)
				{
					return new TrackingBotQuest
					{
						sessionId = sessionId,
						profileId = player.ProfileId,
						time = time,
						questName = "",
						isEFTQuest = false,
						actionType = "",
						status = "None",
						objectiveX = 0f,
						objectiveY = 0f,
						objectiveZ = 0f
					};
				}
				Vector3 questLocation = botQuestInfo.QuestLocation;
				bool flag = !float.IsNegativeInfinity(questLocation.x);
				return new TrackingBotQuest
				{
					sessionId = sessionId,
					profileId = player.ProfileId,
					time = time,
					questName = botQuestInfo.QuestName,
					isEFTQuest = botQuestInfo.IsEftQuest,
					actionType = botQuestInfo.CurrentActionType,
					status = "Active",
					objectiveX = (flag ? questLocation.x : 0f),
					objectiveY = (flag ? questLocation.y : 0f),
					objectiveZ = (flag ? questLocation.z : 0f)
				};
			}
			object obj = ((_getCurrentJobAssignmentMethod.GetParameters().Length != 2) ? _getCurrentJobAssignmentMethod.Invoke(null, new object[1] { val }) : _getCurrentJobAssignmentMethod.Invoke(null, new object[2] { val, false }));
			if (obj == null)
			{
				return null;
			}
			string status = _jobStatusProp?.GetValue(obj)?.ToString() ?? "Unknown";
			string questName = "";
			bool isEFTQuest = false;
			if (_jobQuestAssignmentProp != null)
			{
				object value = _jobQuestAssignmentProp.GetValue(obj);
				if (value != null)
				{
					questName = _questNameProp?.GetValue(value)?.ToString() ?? "";
					if (_questIsEFTQuestProp?.GetValue(value) is bool flag2)
					{
						isEFTQuest = flag2;
					}
				}
			}
			string actionType = "";
			if (_jobObjectiveStepProp != null)
			{
				object value2 = _jobObjectiveStepProp.GetValue(obj);
				if (value2 != null && _stepActionTypeProp != null)
				{
					actionType = _stepActionTypeProp.GetValue(value2)?.ToString() ?? "";
				}
			}
			float objectiveX = 0f;
			float objectiveY = 0f;
			float objectiveZ = 0f;
			if (_jobPositionProp != null)
			{
				object value3 = _jobPositionProp.GetValue(obj);
				if (value3 is Vector3 val2)
				{
					objectiveX = val2.x;
					objectiveY = val2.y;
					objectiveZ = val2.z;
				}
				else if (value3 != null)
				{
					try
					{
						Vector3 val3 = (Vector3)value3;
						objectiveX = val3.x;
						objectiveY = val3.y;
						objectiveZ = val3.z;
					}
					catch
					{
					}
				}
			}
			return new TrackingBotQuest
			{
				sessionId = sessionId,
				profileId = player.ProfileId,
				time = time,
				questName = questName,
				isEFTQuest = isEFTQuest,
				actionType = actionType,
				status = status,
				objectiveX = objectiveX,
				objectiveY = objectiveY,
				objectiveZ = objectiveZ
			};
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogError((object)("RAID_REVIEW :::: QUESTINGBOTS :::: Error extracting quest for " + player.ProfileId + ": " + ex.Message));
			return null;
		}
	}
}
