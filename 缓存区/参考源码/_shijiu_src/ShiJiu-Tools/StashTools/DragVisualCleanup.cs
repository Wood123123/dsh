using System;

namespace StashTools;

internal static class DragVisualCleanup
{
	internal static void Run(Action<Exception> report, params Action[] steps)
	{
		foreach (Action action in steps)
		{
			try
			{
				action();
			}
			catch (Exception obj)
			{
				try
				{
					report(obj);
				}
				catch
				{
				}
			}
		}
	}
}
