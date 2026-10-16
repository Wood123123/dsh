using System;

namespace StashTools;

internal static class PauseClockPolicy
{
	internal static long ElapsedTicks(long started, long now, long frequency)
	{
		if (frequency <= 0)
		{
			throw new ArgumentOutOfRangeException("frequency");
		}
		if (now <= started)
		{
			return 0L;
		}
		double num = ((double)now - (double)started) / (double)frequency * 10000000.0;
		if (double.IsNaN(num) || double.IsInfinity(num) || num > (double)TimeSpan.MaxValue.Ticks)
		{
			throw new InvalidOperationException("暂停计时超出范围");
		}
		return (long)num;
	}
}
