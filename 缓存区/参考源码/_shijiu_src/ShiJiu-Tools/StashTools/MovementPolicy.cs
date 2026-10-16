using System.Collections.Generic;
using System.Linq;

namespace StashTools;

internal static class MovementPolicy
{
	internal static bool A(bool P_0, bool P_1, bool P_2, bool P_3, bool P_4, bool P_5)
	{
		if (!P_0 && !P_1 && !P_2 && !P_3 && !P_4)
		{
			return !P_5;
		}
		return false;
	}

	internal static bool A(int P_0, int P_1, IReadOnlyList<bool> P_2, GridPacking.Box P_3, params GridPacking.Box[] released)
	{
		if (P_0 <= 0 || P_1 <= 0 || P_2.Count < (long)P_0 * (long)P_1 || P_3.W <= 0 || P_3.H <= 0 || P_3.X < 0 || P_3.Y < 0 || P_3.X + P_3.W > P_0 || P_3.Y + P_3.H > P_1)
		{
			return false;
		}
		for (int y = P_3.Y; y < P_3.Y + P_3.H; y++)
		{
			int x;
			for (x = P_3.X; x < P_3.X + P_3.W; x++)
			{
				if (P_2[y * P_0 + x] && !released.Any((GridPacking.Box box) => x >= box.X && x < box.X + box.W && y >= box.Y && y < box.Y + box.H))
				{
					return false;
				}
			}
		}
		return true;
	}

	internal static bool A(int P_0, int P_1, IReadOnlyList<bool> P_2, GridPacking.Box P_3, GridPacking.Box P_4, GridPacking.Box P_5, GridPacking.Box P_6)
	{
		if (!GridPacking.A(P_3, P_4) && A(P_0, P_1, P_2, P_3, P_5, P_6))
		{
			return A(P_0, P_1, P_2, P_4, P_5, P_6);
		}
		return false;
	}
}
