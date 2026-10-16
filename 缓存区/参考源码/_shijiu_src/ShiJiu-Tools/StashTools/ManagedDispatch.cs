using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StashTools;

internal sealed class ManagedDispatch
{
	private readonly object _gate = new object();

	private readonly Queue<Action> _queue = new Queue<Action>();

	private readonly Func<long> _timestamp;

	private readonly long _frequency;

	private bool _accepting = true;

	internal int Count
	{
		get
		{
			lock (_gate)
			{
				return _queue.Count;
			}
		}
	}

	internal ManagedDispatch(Func<long>? timestamp = null, long frequency = 0L)
	{
		_timestamp = timestamp ?? new Func<long>(Stopwatch.GetTimestamp);
		_frequency = ((frequency > 0) ? frequency : Stopwatch.Frequency);
	}

	internal bool Post(Action action)
	{
		if (action == null)
		{
			throw new ArgumentNullException("action");
		}
		lock (_gate)
		{
			if (!_accepting)
			{
				return false;
			}
			_queue.Enqueue(action);
			return true;
		}
	}

	internal void Start()
	{
		lock (_gate)
		{
			_queue.Clear();
			_accepting = true;
		}
	}

	internal void Stop()
	{
		lock (_gate)
		{
			_accepting = false;
			_queue.Clear();
		}
	}

	internal int Drain(int maximum, double milliseconds, Action<Exception> failed)
	{
		if (maximum <= 0 || !double.IsFinite(milliseconds) || milliseconds <= 0.0)
		{
			return 0;
		}
		long num = _timestamp();
		double num2 = (double)_frequency * milliseconds / 1000.0;
		int num3 = 0;
		while (num3 < maximum && (num3 <= 0 || !((double)(_timestamp() - num) >= num2)))
		{
			Action result;
			lock (_gate)
			{
				if (!_accepting || !_queue.TryDequeue(out result))
				{
					break;
				}
				goto IL_008f;
			}
			IL_008f:
			try
			{
				result();
			}
			catch (Exception obj)
			{
				try
				{
					failed(obj);
				}
				catch
				{
				}
			}
			num3++;
		}
		return num3;
	}
}
