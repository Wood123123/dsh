using System;

namespace StashTools.Fika;

internal sealed class FikaPauseRegistration
{
	private double _retryAt;

	internal bool Registered { get; private set; }

	internal bool TryRegister(bool started, double now, Action register, out Exception? error)
	{
		error = null;
		if (Registered)
		{
			return true;
		}
		if (!started || !double.IsFinite(now) || now < 0.0 || now < _retryAt)
		{
			return false;
		}
		_retryAt = now + 1.0;
		try
		{
			register();
			Registered = true;
			return true;
		}
		catch (Exception ex)
		{
			error = ex;
			return false;
		}
	}

	internal void Reset()
	{
		Registered = false;
		_retryAt = 0.0;
	}
}
