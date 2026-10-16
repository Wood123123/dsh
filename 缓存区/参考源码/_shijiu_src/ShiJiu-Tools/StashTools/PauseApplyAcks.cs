using System;
using System.Collections.Generic;

namespace StashTools;

internal sealed class PauseApplyAcks
{
	private readonly HashSet<string> _pending = new HashSet<string>(StringComparer.Ordinal);

	internal long Revision { get; private set; }

	internal bool Paused { get; private set; }

	internal double Deadline { get; private set; }

	internal bool Waiting => _pending.Count > 0;

	internal void Begin(long revision, bool paused, IEnumerable<string> peers, double now)
	{
		Revision = revision;
		Paused = paused;
		Deadline = now + 3.0;
		_pending.Clear();
		foreach (string peer in peers)
		{
			_pending.Add(peer);
		}
	}

	internal bool Accept(string authenticatedId, long revision, bool paused)
	{
		if (revision >= Revision && paused == Paused)
		{
			return _pending.Remove(authenticatedId);
		}
		return false;
	}

	internal bool Expired(double now)
	{
		if (Waiting)
		{
			return now >= Deadline;
		}
		return false;
	}

	internal void Clear()
	{
		_pending.Clear();
		Revision = 0L;
		Deadline = 0.0;
	}
}
