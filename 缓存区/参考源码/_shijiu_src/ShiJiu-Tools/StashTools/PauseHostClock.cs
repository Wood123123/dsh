using System;

namespace StashTools;

internal sealed class PauseHostClock
{
	private double _offset;

	private double _last;

	private double _bestRtt = double.PositiveInfinity;

	internal bool Ready { get; private set; }

	internal bool Observe(double sent, double host, double received)
	{
		double num = received - sent;
		if (!double.IsFinite(sent) || !double.IsFinite(host) || !double.IsFinite(received) || sent < 0.0 || host < 0.0 || num < 0.0 || num > 2.0)
		{
			return false;
		}
		if (!Ready || num <= _bestRtt + 0.02)
		{
			_offset = host - (sent + received) / 2.0;
			_bestRtt = Math.Min(_bestRtt, num);
			Ready = true;
		}
		return true;
	}

	internal double Now(double local)
	{
		if (!Ready || !double.IsFinite(local) || local < 0.0)
		{
			return _last;
		}
		return _last = Math.Max(_last, Math.Max(0.0, local + _offset));
	}
}
