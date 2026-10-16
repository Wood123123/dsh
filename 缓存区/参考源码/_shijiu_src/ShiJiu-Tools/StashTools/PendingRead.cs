using System;

namespace StashTools;

internal sealed class PendingRead
{
	private readonly Func<bool> _current;

	private readonly Func<bool> _completed;

	private readonly Action _finish;

	private readonly Action<Exception> _failed;

	private bool _ended;

	internal PendingRead(Func<bool> current, Func<bool> completed, Action finish, Action<Exception> failed)
	{
		_current = current;
		_completed = completed;
		_finish = finish;
		_failed = failed;
	}

	internal bool Tick()
	{
		if (_ended)
		{
			return true;
		}
		try
		{
			if (!_current())
			{
				_ended = true;
				return true;
			}
			if (!_completed())
			{
				return false;
			}
			_ended = true;
			_finish();
		}
		catch (Exception obj)
		{
			_ended = true;
			_failed(obj);
		}
		return true;
	}
}
