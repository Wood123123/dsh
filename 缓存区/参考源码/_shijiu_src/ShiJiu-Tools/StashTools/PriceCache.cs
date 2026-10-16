using System;
using System.Collections.Generic;

namespace StashTools;

internal sealed class PriceCache
{
	internal sealed class Entry
	{
		internal double Price;

		internal double Expires;

		internal double StaleUntil;

		internal bool Pending;
	}

	private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

	internal Entry Get(string key, double now, Action<Entry> request)
	{
		if (_entries.TryGetValue(key, out Entry value) && now < value.Expires)
		{
			return value;
		}
		if (_entries.Count > 512)
		{
			_entries.Clear();
		}
		value = new Entry
		{
			Price = ((value != null && now < value.StaleUntil) ? value.Price : 0.0),
			StaleUntil = (value?.StaleUntil ?? 0.0),
			Pending = true,
			Expires = now + 10.0
		};
		_entries[key] = value;
		request(value);
		return value;
	}

	internal void Complete(string key, Entry request, double price, double now)
	{
		if (_entries.TryGetValue(key, out Entry value) && value == request)
		{
			request.Pending = false;
			request.Price = ((double.IsFinite(price) && price > 0.0) ? price : 0.0);
			request.Expires = now + (double)((request.Price > 0.0) ? 30 : 5);
			request.StaleUntil = ((request.Price > 0.0) ? (now + 60.0) : 0.0);
		}
	}

	internal void Clear()
	{
		_entries.Clear();
	}
}
