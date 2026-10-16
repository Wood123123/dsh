using System;
using System.Collections.Generic;

namespace StashTools.Fika;

internal sealed class PauseDeferredBuffer<T>
{
	private readonly Queue<(T Value, int Bytes)> _pending = new Queue<(T, int)>();

	private readonly int _maxCount;

	private readonly int _maxBytes;

	internal int Count => _pending.Count;

	internal int Bytes { get; private set; }

	internal PauseDeferredBuffer(int maxCount = 2048, int maxBytes = 4194304)
	{
		if (maxCount <= 0 || maxBytes <= 0)
		{
			throw new ArgumentOutOfRangeException();
		}
		_maxCount = maxCount;
		_maxBytes = maxBytes;
	}

	internal bool TryAdd(T value, int bytes)
	{
		if (bytes < 0 || Count >= _maxCount || bytes > _maxBytes - Bytes)
		{
			return false;
		}
		_pending.Enqueue((value, bytes));
		Bytes += bytes;
		return true;
	}

	internal T Take()
	{
		(T, int) tuple = _pending.Dequeue();
		Bytes -= tuple.Item2;
		return tuple.Item1;
	}

	internal void Clear()
	{
		_pending.Clear();
		Bytes = 0;
	}
}
