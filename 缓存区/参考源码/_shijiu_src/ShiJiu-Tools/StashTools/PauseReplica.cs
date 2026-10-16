using System.Linq;

namespace StashTools;

internal sealed class PauseReplica
{
	internal PauseRoomState? State { get; private set; }

	internal bool Accept(PauseRoomContext context, string authenticatedSender, PauseRoomState state)
	{
		if (!PauseVoting.ValidContext(context) || authenticatedSender != context.HostId || state == null || state.Protocol != 1 || state.RoomId != context.RoomId || state.Revision <= 0 || state.Requester == null || state.Notice == null || (State != null && State.RoomId == state.RoomId && state.Revision <= State.Revision) || !double.IsFinite(state.VoteEndsAt) || state.VoteEndsAt < 0.0 || !double.IsFinite(state.ResumeAt) || state.ResumeAt < 0.0 || !double.IsFinite(state.NoticeUntil) || state.NoticeUntil < 0.0 || state.Agreed == null || state.Agreed.Count > context.Members.Count || state.Agreed.Distinct().Count() != state.Agreed.Count || state.Agreed.Any((string id) => !context.Members.Any((PauseMember m) => m.Id == id)) || (state.Requester.Length > 0 && !context.Members.Any((PauseMember m) => m.Id == state.Requester)) || (state.ResumeAt > 0.0 && (!state.Paused || state.Requester.Length > 0)) || (state.Requester.Length == 0 && (state.VoteEndsAt != 0.0 || state.Agreed.Count != 0)) || (state.Requester.Length > 0 && (state.VoteEndsAt == 0.0 || state.RequestedPause == state.Paused || !state.Agreed.Contains(state.Requester))))
		{
			return false;
		}
		State = state.Copy();
		return true;
	}
}
