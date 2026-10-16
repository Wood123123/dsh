using System;

namespace StashTools;

internal static class MarketPricing
{
	internal static double A(FleaPriceBasis P_0, double P_1, double P_2, double P_3)
	{
		double num = P_0 switch
		{
			FleaPriceBasis.最低价 => P_1, 
			FleaPriceBasis.平均价 => P_2, 
			FleaPriceBasis.最高价 => P_3, 
			_ => 0.0, 
		};
		if (!double.IsFinite(num) || !(num > 0.0) || !(num < 2147483647.0))
		{
			return 0.0;
		}
		return num;
	}

	internal static int A(double P_0, double P_1)
	{
		if (!double.IsFinite(P_0) || P_0 <= 0.0 || !double.IsFinite(P_1) || P_1 <= 0.0)
		{
			return 0;
		}
		double num = P_0 * P_1;
		if (!double.IsFinite(num) || !(num >= 1.0) || !(num <= 2147483647.0))
		{
			return 0;
		}
		return (int)Math.Round(num);
	}
}
