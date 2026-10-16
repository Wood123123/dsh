using System;
using System.Collections.Generic;
using System.Linq;

namespace StashTools;

internal sealed class RaidInventoryObserver
{
	internal readonly record struct Entry(RaidLedger.Sample Item, bool Stackable);

	private readonly Dictionary<string, Entry> _known = new Dictionary<string, Entry>();

	private readonly HashSet<string> _held = new HashSet<string>();

	private readonly RaidLedger _ledger;

	internal RaidInventoryObserver(RaidLedger ledger)
	{
		_ledger = ledger;
	}

	internal void Begin(IEnumerable<Entry> entries)
	{
		_known.Clear();
		_held.Clear();
		Entry[] array = entries.DistinctBy((Entry e) => e.Item.Id).ToArray();
		Entry[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			Entry value = array2[num];
			_known[value.Item.Id] = value;
			_held.Add(value.Item.Id);
		}
		_ledger.A(array.Select((Entry e) => e.Item));
	}

	internal void Resume(IEnumerable<Entry> entries)
	{
		_known.Clear();
		_held.Clear();
		Entry[] array = entries.DistinctBy((Entry e) => e.Item.Id).ToArray();
		Entry[] array2 = array;
		for (int num = 0; num < array2.Length; num++)
		{
			Entry value = array2[num];
			_known[value.Item.Id] = value;
			_held.Add(value.Item.Id);
		}
		_ledger.a(array.Select((Entry e) => e.Item));
	}

	internal void Observe(IEnumerable<Entry> entries, Func<string, double> readDetachedUnits)
	{
		Dictionary<string, Entry> dictionary = entries.DistinctBy((Entry e) => e.Item.Id).ToDictionary((Entry e) => e.Item.Id);
		Dictionary<string, double> dictionary2 = new Dictionary<string, double>();
		Dictionary<string, double> dictionary3 = new Dictionary<string, double>();
		Dictionary<string, double> dictionary4 = _known.ToDictionary<KeyValuePair<string, Entry>, string, double>((KeyValuePair<string, Entry> e) => e.Key, (KeyValuePair<string, Entry> e) => e.Value.Item.Units);
		foreach (string item4 in _held)
		{
			Entry entry = _known[item4];
			if (entry.Stackable)
			{
				double num = (dictionary.TryGetValue(item4, out var value) ? value.Item.Units : readDetachedUnits(item4));
				if (entry.Item.Units > num + 1E-08)
				{
					dictionary2[item4] = entry.Item.Units - num;
				}
				if (!dictionary.ContainsKey(item4))
				{
					Dictionary<string, Entry> known = _known;
					Entry value2 = entry;
					RaidLedger.Sample item = entry.Item;
					item.Units = num;
					value2.Item = item;
					known[item4] = value2;
				}
			}
		}
		foreach (Entry item5 in dictionary.Values.Where((Entry e) => e.Stackable))
		{
			double num2 = item5.Item.Units - dictionary4.GetValueOrDefault(item5.Item.Id);
			if (num2 > 1E-08)
			{
				dictionary3[item5.Item.Id] = num2;
			}
		}
		string[] array = dictionary2.Keys.ToArray();
		foreach (string text in array)
		{
			RaidLedger.Sample item2 = _known[text].Item;
			string[] array2 = dictionary3.Keys.ToArray();
			foreach (string text2 in array2)
			{
				RaidLedger.Sample item3 = dictionary[text2].Item;
				if (!(item2.Template != item3.Template) && !(text == text2))
				{
					double num5 = Math.Min(dictionary2[text], dictionary3[text2]);
					if (!(num5 <= 1E-08))
					{
						RaidLedger ledger = _ledger;
						RaidLedger.Sample item = item2;
						item.Units = dictionary4[text];
						RaidLedger.Sample sample = item;
						item = item3;
						item.Units = dictionary4.GetValueOrDefault(text2);
						ledger.A(sample, item, num5);
						dictionary4[text] -= num5;
						dictionary4[text2] = dictionary4.GetValueOrDefault(text2) + num5;
						dictionary2[text] -= num5;
						dictionary3[text2] -= num5;
					}
				}
			}
		}
		_ledger.a(dictionary.Values.Select((Entry e) => e.Item));
		_held.Clear();
		foreach (Entry value3 in dictionary.Values)
		{
			_known[value3.Item.Id] = value3;
			_held.Add(value3.Item.Id);
		}
	}
}
