using System;

namespace StashTools;

internal readonly struct PauseDateSnapshot
{
	internal DateTime? Value { get; }

	internal PauseDateSnapshot(DateTime? value)
	{
		Value = value;
	}

	internal PauseDateSnapshot Shift(long ticks)
	{
		if (!Value.HasValue)
		{
			return this;
		}
		if (ticks < 0 || Value.Value.Ticks > DateTime.MaxValue.Ticks - ticks)
		{
			throw new InvalidOperationException("战局时间补偿超出范围");
		}
		return new PauseDateSnapshot(Value.Value.AddTicks(ticks));
	}
}
