using System;
using System.Collections.Generic;

namespace StashTools;

internal sealed class InventoryPreparation : IDisposable
{
	private readonly Stack<Action> _undo = new Stack<Action>();

	internal void Record(Action undo)
	{
		_undo.Push(undo);
	}

	internal void Handoff()
	{
		_undo.Clear();
	}

	public void Dispose()
	{
		List<Exception> list = null;
		Action result;
		while (_undo.TryPop(out result))
		{
			try
			{
				result();
			}
			catch (Exception item)
			{
				(list ?? (list = new List<Exception>())).Add(item);
			}
		}
		if (list != null)
		{
			throw new AggregateException("Inventory preparation could not be fully restored", list);
		}
	}
}
