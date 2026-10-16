namespace StashTools;

internal static class HoverPolicy
{
	internal static bool A(bool P_0, bool P_1, bool P_2)
	{
		if (!P_0)
		{
			return !P_1 | P_2;
		}
		return false;
	}

	internal static bool A(bool P_0, bool P_1, bool P_2, bool P_3)
	{
		if (A(P_0, P_1, P_2))
		{
			return !P_3;
		}
		return false;
	}
}
