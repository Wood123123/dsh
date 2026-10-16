using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace StashTools;

internal static class MonoCompatibility
{
	private sealed class Token
	{
		internal readonly IntPtr Value = new IntPtr(Interlocked.Increment(ref _sequence));
	}

	private static long _sequence;

	private static readonly ConditionalWeakTable<object, Token> Tokens = new ConditionalWeakTable<object, Token>();

	internal static IntPtr Identity(this object? value)
	{
		if (value != null)
		{
			return Tokens.GetValue(value, (object _) => new Token()).Value;
		}
		return IntPtr.Zero;
	}

	internal static T? TryCast<T>(this object? value) where T : class
	{
		return value as T;
	}

	internal static T Cast<T>(this object value)
	{
		return (T)value;
	}

	internal static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> source, Func<T, TKey> keySelector)
	{
		HashSet<TKey> keys = new HashSet<TKey>();
		foreach (T item in source)
		{
			if (keys.Add(keySelector(item)))
			{
				yield return item;
			}
		}
	}
}
