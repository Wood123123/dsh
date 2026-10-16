using System.Collections.Generic;

namespace StashTools;

public sealed class PauseRoomState
{
	public int Protocol { get; set; } = 1;

	public string RoomId { get; set; } = "";

	public long Revision { get; set; }

	public bool Paused { get; set; }

	public string Requester { get; set; } = "";

	public bool RequestedPause { get; set; }

	public double VoteEndsAt { get; set; }

	public List<string> Agreed { get; set; } = new List<string>();

	public double ResumeAt { get; set; }

	public string Notice { get; set; } = "";

	public double NoticeUntil { get; set; }

	public PauseRoomState Copy()
	{
		return new PauseRoomState
		{
			Protocol = Protocol,
			RoomId = RoomId,
			Revision = Revision,
			Paused = Paused,
			Requester = Requester,
			RequestedPause = RequestedPause,
			VoteEndsAt = VoteEndsAt,
			Agreed = new List<string>(Agreed),
			ResumeAt = ResumeAt,
			Notice = Notice,
			NoticeUntil = NoticeUntil
		};
	}
}
