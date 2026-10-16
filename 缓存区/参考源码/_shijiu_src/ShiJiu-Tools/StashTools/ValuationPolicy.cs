using System;
using System.Linq;

namespace StashTools;

internal static class ValuationPolicy
{
	internal static bool A(bool P_0, bool P_1)
	{
		return P_0 & P_1;
	}

	internal static double A(double P_0, double P_1)
	{
		return Math.Max((double.IsFinite(P_0) && P_0 > 0.0) ? P_0 : 0.0, (double.IsFinite(P_1) && P_1 > 0.0) ? P_1 : 0.0);
	}

	internal static string A(bool P_0, bool P_1, string P_2, string P_3)
	{
		return string.Join("  ", new string[2]
		{
			P_0 ? ("装备价值：" + P_2) : null,
			P_1 ? ("物资价值：" + P_3) : null
		}.Where((string text) => text != null));
	}
}
