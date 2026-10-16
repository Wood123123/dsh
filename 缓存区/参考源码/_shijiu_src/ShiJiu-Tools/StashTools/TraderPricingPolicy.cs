using System;
using System.Collections.Generic;

namespace StashTools;

internal static class TraderPricingPolicy
{
	private static readonly HashSet<string> Stock = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"54cb50c76803fa8b248b4571", "54cb57776803fa99248b456e", "579dc571d53a0658a154fbec", "58330581ace78e27b8b10cee", "5935c25fb3acc3127c3d8cd9", "5a7c2eca46aef81a7ca2145d", "5ac3b934156ae10c4430e83c", "5c0647fdd443bc2504c2d371", "638f541a29ffd1183d187f57", "656f0f98d80a697f855d34b1",
		"6617beeaa9cfa777ca915b7c", "67f7af56c117b6140af2a607", "6864e812f9fe664cb8b8e152", "688246518448b05efd61d461", "688246958448b05efd61d462", "68fe15910f29ba3fdbba9d54", "68fe15990f29ba3fdbba9d55", "69e0d6cc77b63940375b9173"
	};

	internal static bool Includes(string id, bool includeCustom)
	{
		if (!includeCustom)
		{
			return Stock.Contains(id);
		}
		return true;
	}
}
