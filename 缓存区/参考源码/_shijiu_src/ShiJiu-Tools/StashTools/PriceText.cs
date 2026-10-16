using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StashTools;

internal static class PriceText
{
	internal static double A(double P_0, IEnumerable<double> P_1)
	{
		return P_0 + P_1.Sum();
	}

	internal static string A(double P_0, PriceDisplayUnit P_1 = PriceDisplayUnit.千位k)
	{
		if (!double.IsFinite(P_0) || P_0 < 0.0)
		{
			return "—";
		}
		return "₽" + ((P_1 == PriceDisplayUnit.完整金额) ? Math.Round(P_0).ToString("#,0", CultureInfo.InvariantCulture) : ((P_1 == PriceDisplayUnit.自动k或m && P_0 >= 1000000.0) ? ((P_0 / 1000000.0).ToString("0.#", CultureInfo.InvariantCulture) + "m") : ((P_0 >= 1000.0) ? ((P_0 / 1000.0).ToString("#,0.#", CultureInfo.InvariantCulture) + "k") : Math.Round(P_0).ToString("0", CultureInfo.InvariantCulture))));
	}

	internal static bool A(double P_0, double? P_1)
	{
		if (double.IsFinite(P_0) && P_0 > 0.0)
		{
			if (P_1.HasValue && double.IsFinite(P_1.Value) && !(P_1.Value <= 0.0))
			{
				return P_0 > P_1.Value;
			}
			return true;
		}
		return false;
	}

	internal static string A(string P_0, double P_1, int P_2, int P_3, bool P_4, bool P_5 = false, PriceDisplayUnit P_6 = PriceDisplayUnit.千位k, bool P_7 = false)
	{
		double num = P_1 / (double)Math.Max(1, P_2) / (double)Math.Max(1, P_3);
		string text = A(num, P_6);
		string text2 = A(P_1, P_6);
		if (P_5)
		{
			text = A(text, num);
		}
		if (!P_4)
		{
			string text3 = (P_5 ? A(text2, num) : text2);
			return P_0 + ": " + (P_7 ? ("<b>" + text3 + "</b>") : text3);
		}
		if (P_3 <= 1 && P_2 <= 1)
		{
			return P_0 + ": " + (P_7 ? ("<b>" + text + "</b>") : text);
		}
		return P_0 + ": " + text + " 总计: " + (P_7 ? ("<b>" + text2 + "</b>") : text2);
	}

	internal static string A(string P_0, double P_1)
	{
		string text = ((P_1 < 900.0) ? "9E9E9E" : ((P_1 < 12000.0) ? "FFFFFF" : ((P_1 < 21000.0) ? "1FFF00" : ((P_1 < 38000.0) ? "0070DE" : ((P_1 < 92000.0) ? "A336ED" : "FF8000")))));
		return "<color=#" + text + ">" + P_0 + "</color>";
	}

	internal static string A(string P_0, string P_1, double? P_2, bool P_3)
	{
		if (!P_3 || !P_2.HasValue || string.IsNullOrEmpty(P_1))
		{
			return P_0;
		}
		int num = P_0.IndexOf(P_1, StringComparison.Ordinal);
		if (num < 0)
		{
			return P_0;
		}
		string text = P_0.Substring(0, num);
		string text2 = A(P_1, P_2.Value);
		int num2 = num + P_1.Length;
		return text + text2 + P_0.Substring(num2, P_0.Length - num2);
	}
}
