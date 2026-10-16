using System;

namespace WTTClientCommonLib.Helpers;

public static class StackCountDisplayHelper
{
	public static string GetShortBalance(int count, string itemTpl)
	{
		string text;
		if (count < 1000000)
		{
			if (count < 1000)
			{
				text = count.ToString();
			}
			else
			{
				double num = Math.Round((double)count / 1000.0, 1);
				if (num >= 1000.0)
				{
					return "1.0M";
				}
				text = num.ToString("0.0") + "K";
			}
		}
		else if (count < 1000000000)
		{
			double num2 = Math.Round((double)count / 1000000.0, 1);
			if (num2 >= 1000.0)
			{
				return "1.0B";
			}
			text = num2.ToString("0.0") + "M";
		}
		else
		{
			text = Math.Round((double)count / 1000000000.0, 1).ToString("0.0") + "B";
		}
		string currencySymbol = GetCurrencySymbol(itemTpl);
		if (currencySymbol.Length > 0)
		{
			return text + " " + currencySymbol;
		}
		return text;
	}

	private static string GetCurrencySymbol(string itemTpl)
	{
		return itemTpl switch
		{
			"5449016a4bdc2d6f028b456f" => "₽", 
			"5696686a4bdc2da3298b456a" => "$", 
			"569668774bdc2da2298b4568" => "€", 
			_ => "", 
		};
	}
}
