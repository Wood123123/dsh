using System;
using EFT.Weather;
using Fika.Core.Main.GameMode;
using HarmonyLib;
using UnityEngine;

namespace Manimal.Icebreaker.Fika;

[HarmonyPatch(typeof(HostGameController), "GenerateWeathers")]
internal static class Patch_HostWeatherDiag
{
	[HarmonyPrefix]
	private static void Prefix()
	{
		if (IceGate.On)
		{
			try
			{
				Patch_EnsureEnvironmentManager.EnsureEnvAndWeather();
			}
			catch (Exception ex)
			{
				FikaAddonPlugin.Log.LogWarning((object)("[IceWeather] ensure failed: " + ex.Message));
			}
			FikaAddonPlugin.Log.LogDebug((object)("[IceWeather] host GenerateWeathers — WeatherController.Instance=" + (((Object)(object)WeatherController.Instance != (Object)null) ? "OK" : "NULL (WeatherClasses will stay empty!)")));
		}
	}
}
