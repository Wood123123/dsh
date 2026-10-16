using System.Runtime.CompilerServices;

namespace CookingGrenades;

internal static class PacketCookTimeStore
{
	private sealed class CookTimeRef
	{
		public float Value;
	}

	private static readonly ConditionalWeakTable<object, CookTimeRef> Store = new ConditionalWeakTable<object, CookTimeRef>();

	public static void Set(object packet, float cookTime)
	{
		if (Store.TryGetValue(packet, out var value))
		{
			value.Value = cookTime;
			return;
		}
		Store.Add(packet, new CookTimeRef
		{
			Value = cookTime
		});
	}

	public static bool TryGet(object packet, out float cookTime)
	{
		if (Store.TryGetValue(packet, out var value))
		{
			cookTime = value.Value;
			return true;
		}
		cookTime = 0f;
		return false;
	}

	public static void Remove(object packet)
	{
		Store.Remove(packet);
	}
}
