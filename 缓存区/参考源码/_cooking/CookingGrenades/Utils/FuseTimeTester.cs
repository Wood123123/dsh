using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using CookingGrenades.Config;
using UnityEngine;

namespace CookingGrenades.Utils;

internal class FuseTimeTester
{
	public static void Init()
	{
		ConfigEntry<bool> timeSimulationOutput = ConfigManager.TimeSimulationOutput;
		timeSimulationOutput.SettingChanged += delegate
		{
			if (timeSimulationOutput.Value)
			{
				RunFuseTimeTest(ConfigManager.TimeSimulationValue.Value);
				timeSimulationOutput.Value = false;
			}
		};
	}

	public static void RunFuseTimeTest(float tsetValue)
	{
		float[] array = new float[ConfigManager.FuseTimeTestCount.Value];
		Dictionary<float, int> dictionary = new Dictionary<float, int>();
		float num = float.MaxValue;
		float num2 = float.MinValue;
		float num3 = 0f;
		float num6 = default(float);
		int num7 = default(int);
		for (int i = 0; i < ConfigManager.FuseTimeTestCount.Value; i++)
		{
			float num4 = (array[i] = MathUtils.GenerateNormalRandomFast(tsetValue, tsetValue * ConfigManager.FuseTimeSpreadFactor.Value));
			float num5 = (float)Math.Round(num4, 1);
			if (dictionary.ContainsKey(num5))
			{
				num6 = num5;
				num7 = dictionary[num6]++;
			}
			else
			{
				dictionary.Add(num5, 1);
			}
			num = Mathf.Min(num, num4);
			num2 = Mathf.Max(num2, num4);
			num3 += num4;
		}
		float num8 = num3 / (float)ConfigManager.FuseTimeTestCount.Value;
		float num9 = 0f;
		for (int j = 0; j < ConfigManager.FuseTimeTestCount.Value; j++)
		{
			num9 += Mathf.Pow(array[j] - num8, 2f);
		}
		float num10 = Mathf.Sqrt(num9 / (float)ConfigManager.FuseTimeTestCount.Value);
		Plugin.log.LogInfo((object)"=== Distribution Test Results ===");
		Plugin.log.LogInfo((object)$"Target Value: {tsetValue}, FuseTime Spread Factor: {ConfigManager.FuseTimeSpreadFactor.Value}, Test Count {ConfigManager.FuseTimeTestCount.Value}");
		Plugin.log.LogInfo((object)$"Minimum Time: {num:F3} seconds");
		Plugin.log.LogInfo((object)$"Maximum Time: {num2:F3} seconds");
		Plugin.log.LogInfo((object)$"Average Time: {num8:F3} seconds");
		Plugin.log.LogInfo((object)$"Standard Deviation: {num10:F3} seconds");
		Plugin.log.LogInfo((object)"=== Frequency Distribution ===");
		foreach (KeyValuePair<float, int> item in dictionary.OrderBy((KeyValuePair<float, int> x) => x.Key))
		{
			MyExtensions.Deconstruct<float, int>(item, ref num6, ref num7);
			float num11 = num6;
			int num12 = num7;
			float num13 = (float)num12 / (float)ConfigManager.FuseTimeTestCount.Value * 100f;
			Plugin.log.LogInfo((object)$"Time: {num11,5:F2}, Count: {num12,5}, Percentage: {num13,6:F2}%");
		}
	}
}
