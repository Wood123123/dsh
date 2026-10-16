using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace WTTClientCommonLib.Components;

internal static class SalvageZoneTracker
{
	private static readonly Dictionary<Player, SalvageItemTrigger> _active = new Dictionary<Player, SalvageItemTrigger>();

	public static void Set(Player player, SalvageItemTrigger zone)
	{
		if (!((Object)(object)player == (Object)null))
		{
			_active[player] = zone;
		}
	}

	public static void Clear(Player player, SalvageItemTrigger zone)
	{
		if (!((Object)(object)player == (Object)null) && _active.TryGetValue(player, out SalvageItemTrigger value) && (Object)(object)value == (Object)(object)zone)
		{
			_active.Remove(player);
		}
	}

	public static SalvageItemTrigger Get(Player player)
	{
		if ((Object)(object)player == (Object)null)
		{
			return null;
		}
		if (!_active.TryGetValue(player, out SalvageItemTrigger value))
		{
			return null;
		}
		return value;
	}
}
