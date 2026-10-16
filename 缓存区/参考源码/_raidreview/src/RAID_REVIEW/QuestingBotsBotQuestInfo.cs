using System.Runtime.CompilerServices;
using UnityEngine;

namespace RAID_REVIEW;

internal class QuestingBotsBotQuestInfo
{
	[CompilerGenerated]
	private Vector3 _003CQuestLocation_003Ek__BackingField;

	public bool IsValid { get; private set; }

	public string CurrentDecision { get; private set; }

	public string CurrentActionType { get; private set; }

	public string QuestName { get; private set; }

	public Vector3 QuestLocation
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CQuestLocation_003Ek__BackingField;
		}
		[CompilerGenerated]
		private set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CQuestLocation_003Ek__BackingField = value;
		}
	}

	public bool IsEftQuest { get; private set; }

	public bool HasAQuest => QuestName != string.Empty;

	public QuestingBotsBotQuestInfo()
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		CurrentDecision = string.Empty;
		CurrentActionType = string.Empty;
		QuestName = string.Empty;
		QuestLocation = Vector3.negativeInfinity;
		base._002Ector();
	}

	public QuestingBotsBotQuestInfo(string currentDecision, string currentActionType)
		: this()
	{
		CurrentDecision = currentDecision;
		CurrentActionType = currentActionType;
		IsValid = true;
	}

	public QuestingBotsBotQuestInfo(string currentDecision, string currentActionType, string questName, Vector3 questLocation, bool isEftQuest)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		this._002Ector(currentDecision, currentActionType);
		QuestName = questName;
		QuestLocation = questLocation;
		IsEftQuest = isEftQuest;
	}
}
