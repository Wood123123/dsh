using System;

namespace StashTools;

internal sealed class FleaSettingOverride
{
	internal sealed record Binding((long Session, long Settings) Key, bool OnlyFir, Action<bool> Write);

	internal enum Change
	{
		None,
		Applied,
		AlreadyAllowed,
		Restored,
		Released,
		ExternalChange
	}

	private (long Session, long Settings) _key;

	private bool _baseline;

	private bool _externalChange;

	internal bool Tracking { get; private set; }

	internal Change Update(bool enabled, Func<Binding?> resolve)
	{
		if (!enabled && !Tracking)
		{
			return Change.None;
		}
		Binding binding = resolve();
		if (binding == null)
		{
			return Change.None;
		}
		if (Tracking)
		{
			(long, long) key = binding.Key;
			(long, long) key2 = _key;
			if (key.Item1 != key2.Item1 || key.Item2 != key2.Item2)
			{
				Reset();
			}
		}
		if (!enabled)
		{
			if (!Tracking)
			{
				return Change.None;
			}
			int num;
			if (_baseline && !_externalChange)
			{
				num = ((!binding.OnlyFir) ? 1 : 0);
				if (num != 0)
				{
					binding.Write(_baseline);
				}
			}
			else
			{
				num = 0;
			}
			Reset();
			if (num == 0)
			{
				return Change.Released;
			}
			return Change.Restored;
		}
		if (!Tracking)
		{
			if (binding.OnlyFir)
			{
				binding.Write(obj: false);
			}
			_key = binding.Key;
			_baseline = binding.OnlyFir;
			Tracking = true;
			if (!_baseline)
			{
				return Change.AlreadyAllowed;
			}
			return Change.Applied;
		}
		if (binding.OnlyFir && !_externalChange)
		{
			_externalChange = true;
			return Change.ExternalChange;
		}
		return Change.None;
	}

	private void Reset()
	{
		Tracking = false;
		_externalChange = false;
	}
}
