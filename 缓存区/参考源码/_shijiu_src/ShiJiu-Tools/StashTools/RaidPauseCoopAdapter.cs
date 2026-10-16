using System;

namespace StashTools;

public sealed class RaidPauseCoopAdapter
{
	public Func<PauseRoomContext?> Capture { get; init; }

	public Action<long> SendPressToHost { get; init; }

	public Action<PauseRoomState> BroadcastFromHost { get; init; }

	public Action<bool> SetGameplayPaused { get; init; }

	public Action<string> ReportFailure { get; init; }
}
