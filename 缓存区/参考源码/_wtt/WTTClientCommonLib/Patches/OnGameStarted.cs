using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using SPT.Reflection.Patching;
using UnityEngine;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.Patches;

internal class OnGameStarted : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return typeof(GameWorld).GetMethod("OnGameStarted", BindingFlags.Instance | BindingFlags.Public);
	}

	[PatchPostfix]
	private static void PatchPostfix(GameWorld __instance)
	{
		try
		{
			string currentMap = __instance.LocationId;
			List<CustomQuestZone> zones = QuestZones.GetZones();
			if (zones == null || zones.Count == 0)
			{
				LogHelper.LogDebug("No zones data loaded; skipping initialization.");
				return;
			}
			QuestZones.CreateZones(ZoneConfigManager.ExistingQuestZones = zones.Where((CustomQuestZone zone) => zone.ZoneLocation.ToLower() == currentMap.ToLower()).ToList());
			Player mainPlayer = __instance.MainPlayer;
			if ((Object)(object)mainPlayer == (Object)null)
			{
				LogHelper.LogError("MainPlayer is null in OnGameStarted");
				return;
			}
			string locationId = __instance.LocationId;
			if (mainPlayer.Profile == null)
			{
				LogHelper.LogError("Player.Profile is null");
				return;
			}
			if (mainPlayer.Profile.QuestsData == null)
			{
				LogHelper.LogError("Player.Profile.QuestsData is null");
				return;
			}
			if (string.IsNullOrEmpty(locationId))
			{
				LogHelper.LogError("LocationId is null/empty");
				return;
			}
			WTTClientCommonLib instance = WTTClientCommonLib.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				LogHelper.LogError("Instance is null in OnGameStarted");
				return;
			}
			AssetLoader assetLoader = instance.AssetLoader;
			if (assetLoader == null)
			{
				LogHelper.LogError("AssetLoader is null in OnGameStarted");
				return;
			}
			List<CustomSpawnConfig> spawnConfigs = assetLoader.SpawnConfigs;
			if (spawnConfigs == null || spawnConfigs.Count == 0)
			{
				LogHelper.LogDebug("No SpawnConfigs in OnGameStarted");
				return;
			}
			LogHelper.LogDebug($"Processing {spawnConfigs.Count} spawn configs for {locationId}");
			foreach (CustomSpawnConfig item in spawnConfigs)
			{
				assetLoader.ProcessSpawnConfig(__instance.MainPlayer, item, locationId);
			}
		}
		catch (Exception ex)
		{
			ModulePatch.Logger.LogError((object)ex);
		}
	}
}
