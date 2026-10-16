using System;
using System.Collections.Generic;
using System.Linq;

namespace StashTools;

internal sealed class PauseVoting
{
	internal const double VoteSeconds = 10.0;

	internal const double ResumeSeconds = 3.0;

	internal const double RequestCooldown = 15.0;

	private readonly string _host;

	private HashSet<string> _members;

	private readonly Dictionary<string, long> _sequences = new Dictionary<string, long>();

	private readonly Dictionary<string, double> _cooldowns = new Dictionary<string, double>();

	private readonly PauseRoomState _state;

	internal PauseRoomState Snapshot => _state.Copy();

	internal PauseVoting(PauseRoomContext context)
	{
		if (!ValidContext(context))
		{
			throw new ArgumentException("暂停房间信息无效");
		}
		_host = context.HostId;
		_members = context.Members.Select((PauseMember m) => m.Id).ToHashSet();
		_state = new PauseRoomState
		{
			RoomId = context.RoomId,
			Revision = 1L
		};
	}

	internal static bool ValidContext(PauseRoomContext? c)
	{
		if (c != null && !string.IsNullOrWhiteSpace(c.RoomId) && !string.IsNullOrWhiteSpace(c.HostId) && c.Members != null)
		{
			int count = c.Members.Count;
			if (count > 0 && count <= 64 && c.Members.All((PauseMember m) => m != null && !string.IsNullOrWhiteSpace(m.Id)) && c.Members.Select((PauseMember m) => m.Id).Distinct().Count() == c.Members.Count && c.Members.Any((PauseMember m) => m.Id == c.HostId) && c.Members.Any((PauseMember m) => m.Id == c.LocalId) && double.IsFinite(c.HostTime))
			{
				return c.HostTime >= 0.0;
			}
		}
		return false;
	}

	private void ClearVote()
	{
		_state.Requester = "";
		_state.Agreed.Clear();
		_state.VoteEndsAt = 0.0;
	}

	private void Notice(string message, double now)
	{
		_state.Notice = message;
		_state.NoticeUntil = now + 4.0;
	}

	private void Commit(bool pause, double now)
	{
		ClearVote();
		_state.ResumeAt = 0.0;
		if (pause)
		{
			_state.Paused = true;
			Notice("战局已暂停", now);
		}
		else
		{
			_state.ResumeAt = now + 3.0;
			Notice("准备恢复战局", now);
		}
		_state.Revision++;
	}

	internal bool Press(string sender, long sequence, double now)
	{
		if (!double.IsFinite(now) || now < 0.0 || !_members.Contains(sender) || sequence <= 0 || sequence <= _sequences.GetValueOrDefault(sender))
		{
			return false;
		}
		_sequences[sender] = sequence;
		bool flag = _state.Requester.Length > 0 && now >= _state.VoteEndsAt;
		Tick(now);
		if (sender == _host)
		{
			if (_state.ResumeAt > 0.0)
			{
				_state.ResumeAt = 0.0;
				Notice("房主取消了恢复倒计时", now);
				_state.Revision++;
			}
			else
			{
				Commit(!_state.Paused, now);
			}
			return true;
		}
		if (flag)
		{
			return false;
		}
		if (_state.ResumeAt > 0.0)
		{
			return false;
		}
		if (_state.Requester.Length > 0)
		{
			if (_state.Agreed.Contains(sender))
			{
				return false;
			}
			_state.Agreed.Add(sender);
			_state.Revision++;
			if (_members.All(_state.Agreed.Contains))
			{
				Commit(_state.RequestedPause, now);
			}
			return true;
		}
		if (now < _cooldowns.GetValueOrDefault(sender))
		{
			return false;
		}
		_cooldowns[sender] = now + 15.0;
		_state.Requester = sender;
		_state.RequestedPause = !_state.Paused;
		_state.VoteEndsAt = now + 10.0;
		_state.Agreed.Add(sender);
		_state.Notice = "";
		_state.NoticeUntil = 0.0;
		_state.Revision++;
		return true;
	}

	internal bool Tick(double now)
	{
		if (!double.IsFinite(now) || now < 0.0)
		{
			return false;
		}
		if (_state.ResumeAt > 0.0 && now >= _state.ResumeAt)
		{
			_state.ResumeAt = 0.0;
			_state.Paused = false;
			Notice("战局已恢复", now);
			_state.Revision++;
			return true;
		}
		if (_state.Requester.Length > 0 && now >= _state.VoteEndsAt)
		{
			ClearVote();
			Notice("申请超时，已取消", now);
			_state.Revision++;
			return true;
		}
		return false;
	}

	internal bool ResumeAfterFailure(string authenticatedSender, double now)
	{
		if (!_members.Contains(authenticatedSender) || !double.IsFinite(now) || now < 0.0)
		{
			return false;
		}
		ClearVote();
		_state.ResumeAt = 0.0;
		_state.Paused = false;
		Notice("暂停同步中断，已恢复战局", now);
		_state.Revision++;
		return true;
	}

	internal bool UpdateMembers(IEnumerable<string> members, double now)
	{
		HashSet<string> hashSet = members.ToHashSet();
		int count = hashSet.Count;
		bool flag = ((count < 1 || count > 64) ? true : false);
		if (flag || !hashSet.Contains(_host) || hashSet.Any(string.IsNullOrWhiteSpace))
		{
			throw new ArgumentException("暂停成员名单无效");
		}
		if (_members.SetEquals(hashSet))
		{
			return false;
		}
		_members = hashSet;
		ClearVote();
		Notice("成员发生变化，已取消申请", now);
		_state.Revision++;
		return true;
	}
}
