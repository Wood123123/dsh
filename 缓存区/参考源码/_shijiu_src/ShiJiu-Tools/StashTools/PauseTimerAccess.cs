using System;
using EFT;

namespace StashTools;

internal static class PauseTimerAccess
{
	internal static (PauseDateSnapshot Start, PauseDateSnapshot Escape) Read(GameTimer timer)
	{
		if (timer == null)
		{
			throw new ArgumentNullException("timer");
		}
		DateTime? startDateTime = timer._startDateTime;
		DateTime? escapeDateTime = timer._escapeDateTime;
		if (!startDateTime.HasValue || startDateTime.Value.Ticks < new DateTime(1970, 1, 1).Ticks || (escapeDateTime.HasValue && escapeDateTime.Value.Ticks < startDateTime.Value.Ticks))
		{
			throw new InvalidOperationException("战局计时器尚未准备好或时间值无效，未执行暂停");
		}
		return (Start: new PauseDateSnapshot(startDateTime), Escape: new PauseDateSnapshot(escapeDateTime));
	}

	internal static void Write(GameTimer timer, PauseDateSnapshot start, PauseDateSnapshot escape)
	{
		if (timer == null)
		{
			throw new ArgumentNullException("timer");
		}
		timer._startDateTime = start.Value;
		timer._escapeDateTime = escape.Value;
	}
}
