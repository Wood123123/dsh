using System.Runtime.CompilerServices;

namespace CookingGrenades;

internal static class ObservedControllerCookStore
{
	private sealed class ControllerRef
	{
		public float Value;
	}

	private static readonly object StoreLock = new object();

	private static readonly ConditionalWeakTable<object, ControllerRef> ControllerCookTimes = new ConditionalWeakTable<object, ControllerRef>();

	public static void Set(object controller, float cookTime)
	{
		if (controller == null)
		{
			return;
		}
		lock (StoreLock)
		{
			if (ControllerCookTimes.TryGetValue(controller, out var value))
			{
				value.Value = cookTime;
				return;
			}
			ControllerCookTimes.Add(controller, new ControllerRef
			{
				Value = cookTime
			});
		}
	}

	public static float Take(object controller)
	{
		if (controller == null)
		{
			return 0f;
		}
		lock (StoreLock)
		{
			float result = 0f;
			if (ControllerCookTimes.TryGetValue(controller, out var value))
			{
				result = value.Value;
				ControllerCookTimes.Remove(controller);
			}
			return result;
		}
	}

	public static void Remove(object controller)
	{
		if (controller == null)
		{
			return;
		}
		lock (StoreLock)
		{
			ControllerCookTimes.Remove(controller);
		}
	}
}
