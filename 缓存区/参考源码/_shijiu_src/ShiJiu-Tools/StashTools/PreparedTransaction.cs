using System;

namespace StashTools;

internal static class PreparedTransaction
{
	internal static bool A(Action P_0, Func<bool> P_1, Action P_2)
	{
		bool flag;
		try
		{
			flag = P_1();
		}
		finally
		{
			P_0();
		}
		if (!flag)
		{
			return false;
		}
		P_2();
		return true;
	}
}
