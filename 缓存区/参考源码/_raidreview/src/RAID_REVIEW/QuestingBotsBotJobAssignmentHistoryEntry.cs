namespace RAID_REVIEW;

internal class QuestingBotsBotJobAssignmentHistoryEntry
{
	public bool IsValid { get; private set; }

	public string StartTimestampText { get; private set; } = string.Empty;

	public string EndTimestampText { get; private set; } = string.Empty;

	public string QuestName { get; private set; } = string.Empty;

	public string QuestObjectiveName { get; private set; } = string.Empty;

	public string QuestStep { get; private set; } = string.Empty;

	public string Status { get; private set; } = string.Empty;

	public long StartTimestamp => long.Parse(StartTimestampText);

	public long EndTimestamp => long.Parse(EndTimestampText);

	public QuestingBotsBotJobAssignmentHistoryEntry()
	{
	}

	public QuestingBotsBotJobAssignmentHistoryEntry(string startTimestamp, string endTimestamp, string questName, string questObjectiveName, string questStep, string status)
		: this()
	{
		StartTimestampText = startTimestamp;
		EndTimestampText = endTimestamp;
		QuestName = questName;
		QuestObjectiveName = questObjectiveName;
		QuestStep = questStep;
		Status = status;
		IsValid = true;
	}
}
