using System;
using System.Collections.Generic;

namespace StashTools;

internal static class InventoryTraversal
{
	internal static void Walk<T>(T root, Func<T, string> id, Func<T, IEnumerable<T>> children, Action<T> visit, bool includeRoot = true)
	{
		HashSet<string> hashSet = new HashSet<string>();
		Stack<(T, bool)> stack = new Stack<(T, bool)>();
		stack.Push((root, includeRoot));
		while (stack.Count > 0)
		{
			(T, bool) tuple = stack.Pop();
			if (!hashSet.Add(id(tuple.Item1)))
			{
				continue;
			}
			if (tuple.Item2)
			{
				visit(tuple.Item1);
			}
			foreach (T item in children(tuple.Item1))
			{
				stack.Push((item, true));
			}
		}
	}
}
