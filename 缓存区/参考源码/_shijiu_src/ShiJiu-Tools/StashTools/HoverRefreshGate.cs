using System;

namespace StashTools;

internal sealed class HoverRefreshGate
{
	private volatile bool _dirty = true;

	private double _expires;

	internal void Invalidate()
	{
		_dirty = true;
	}

	internal bool Due(double now)
	{
		if (!_dirty)
		{
			return now >= _expires;
		}
		return true;
	}

	internal void Begin(double now)
	{
		_dirty = false;
		_expires = now + 30.0;
	}

	internal void Track(double expires)
	{
		_expires = Math.Min(_expires, expires);
	}

	internal void Backoff(double now)
	{
		_dirty = false;
		_expires = now + 3.0;
	}
}
