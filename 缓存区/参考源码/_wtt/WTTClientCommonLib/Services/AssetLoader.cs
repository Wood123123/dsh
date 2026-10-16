using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.Quests;
using Newtonsoft.Json;
using SPT.Custom.Utils;
using UnityEngine;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Services;

public class AssetLoader(ManualLogSource logger)
{
	private readonly ConcurrentDictionary<string, AssetBundle> _loadedBundles = new ConcurrentDictionary<string, AssetBundle>();

	private readonly ConcurrentDictionary<string, string> _bundleKeyLookup = new ConcurrentDictionary<string, string>();

	public List<CustomSpawnConfig> SpawnConfigs;

	public List<CustomSpawnConfig> FetchSpawnConfigs()
	{
		try
		{
			return Utils.Get<List<CustomSpawnConfig>>("/wttcommonlib/spawnsystem/configs/get") ?? new List<CustomSpawnConfig>();
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error fetching configs: {arg}");
			return new List<CustomSpawnConfig>();
		}
	}

	public void ProcessSpawnConfig(Player player, CustomSpawnConfig config, string locationID)
	{
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_012f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (string.IsNullOrEmpty(config.PrefabName) || string.IsNullOrEmpty(config.BundleName) || string.IsNullOrEmpty(config.LocationID))
			{
				LogHelper.LogDebug("[WTT-SpawnSystem] Invalid config: " + JsonConvert.SerializeObject((object)config));
			}
			else
			{
				if (!locationID.Equals(config.LocationID, StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				QuestDataClass quest = null;
				if (!string.IsNullOrEmpty(config.QuestId))
				{
					quest = player.Profile.QuestsData.FirstOrDefault((QuestDataClass q) => q.Id == config.QuestId);
				}
				if (!EvaluateConditions(player, quest, config))
				{
					LogHelper.LogDebug("[WTT-SpawnSystem] Conditions not met for " + config.PrefabName);
					return;
				}
				GameObject val = LoadPrefabFromBundle(config.BundleName, config.PrefabName);
				if (!((Object)(object)val == (Object)null))
				{
					Quaternion rotation = Quaternion.Euler(config.Rotation);
					if (WTTClientCommonLib.FikaInstalled)
					{
						WTTClientCommonLib.SendFikaPacket(config, rotation);
					}
					SpawnPrefab(val, config.Position, rotation);
					LogHelper.LogDebug("[WTT-SpawnSystem] Spawned " + config.PrefabName);
				}
			}
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"[WTT-SpawnSystem] Config processing failed: {arg}");
		}
	}

	private bool EvaluateConditions(Player player, QuestDataClass quest, CustomSpawnConfig config)
	{
		//IL_0279: Unknown result type (might be due to invalid IL or missing references)
		//IL_027e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0469: Unknown result type (might be due to invalid IL or missing references)
		//IL_046e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0477: Unknown result type (might be due to invalid IL or missing references)
		//IL_040c: Unknown result type (might be due to invalid IL or missing references)
		if (config.QuestMustExist.HasValue)
		{
			bool flag = quest != null;
			if (flag != config.QuestMustExist.Value)
			{
				LogHelper.LogDebug($"[CONDITION] Quest existence check failed. Expected: {config.QuestMustExist}, Actual: {flag}");
				return false;
			}
		}
		List<string> requiredQuestStatuses = config.RequiredQuestStatuses;
		if (requiredQuestStatuses != null && requiredQuestStatuses.Count > 0)
		{
			if (quest == null)
			{
				LogHelper.LogDebug("[CONDITION] Required statuses but quest doesn't exist");
				return false;
			}
			bool flag2 = false;
			List<string> list = new List<string>();
			foreach (string requiredQuestStatus in config.RequiredQuestStatuses)
			{
				if (Enum.TryParse<EQuestStatus>(requiredQuestStatus, out EQuestStatus result))
				{
					list.Add(requiredQuestStatus);
					if (quest.Status == result)
					{
						flag2 = true;
					}
				}
			}
			if (!flag2)
			{
				LogHelper.LogDebug(string.Format("[CONDITION] None of required statuses matched: {0}. Actual: {1}", string.Join(", ", list), quest.Status));
				return false;
			}
		}
		requiredQuestStatuses = config.ExcludedQuestStatuses;
		if (requiredQuestStatuses != null && requiredQuestStatuses.Count > 0 && quest != null)
		{
			foreach (string excludedQuestStatus in config.ExcludedQuestStatuses)
			{
				if (Enum.TryParse<EQuestStatus>(excludedQuestStatus, out EQuestStatus result2) && quest.Status == result2)
				{
					LogHelper.LogDebug($"[CONDITION] Excluded status matched: {result2}");
					return false;
				}
			}
		}
		if (!string.IsNullOrEmpty(config.RequiredItemInInventory) && !player.Profile.Inventory.AllRealPlayerItems.Any(delegate(Item item)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			return item.TemplateId == MongoID.op_Implicit(config.RequiredItemInInventory);
		}))
		{
			LogHelper.LogDebug("[CONDITION] Missing required item: " + config.RequiredItemInInventory);
			return false;
		}
		if (config.RequiredLevel.HasValue && player.Profile.Info.Level < config.RequiredLevel.Value)
		{
			LogHelper.LogDebug($"[CONDITION] Level too low. Required: {config.RequiredLevel}, Actual: {player.Profile.Info.Level}");
			return false;
		}
		if (!string.IsNullOrEmpty(config.RequiredFaction))
		{
			string text = ((object)player.Profile.Side/*cast due to constrained. prefix*/).ToString();
			if (!text.Equals(config.RequiredFaction, StringComparison.OrdinalIgnoreCase))
			{
				LogHelper.LogDebug("[CONDITION] Wrong faction. Required: " + config.RequiredFaction + ", Actual: " + text);
				return false;
			}
		}
		if (!string.IsNullOrEmpty(config.LinkedQuestId))
		{
			QuestDataClass val = player.Profile.QuestsData.FirstOrDefault((QuestDataClass q) => q.Id == config.LinkedQuestId);
			if (config.LinkedQuestMustExist.HasValue)
			{
				bool flag3 = val != null;
				if (flag3 != config.LinkedQuestMustExist.Value)
				{
					LogHelper.LogDebug($"[CONDITION] Linked quest existence check failed. Expected: {config.LinkedQuestMustExist}, Actual: {flag3}");
					return false;
				}
			}
			requiredQuestStatuses = config.LinkedRequiredStatuses;
			if (requiredQuestStatuses != null && requiredQuestStatuses.Count > 0)
			{
				if (val == null)
				{
					LogHelper.LogDebug("[CONDITION] Required linked statuses but quest doesn't exist");
					return false;
				}
				bool flag4 = false;
				List<string> list2 = new List<string>();
				foreach (string linkedRequiredStatus in config.LinkedRequiredStatuses)
				{
					if (Enum.TryParse<EQuestStatus>(linkedRequiredStatus, out EQuestStatus result3))
					{
						list2.Add(linkedRequiredStatus);
						if (val.Status == result3)
						{
							flag4 = true;
						}
					}
				}
				if (!flag4)
				{
					LogHelper.LogDebug(string.Format("[CONDITION] None of linked required statuses matched: {0}. Actual: {1}", string.Join(", ", list2), val?.Status));
					return false;
				}
			}
			requiredQuestStatuses = config.LinkedExcludedStatuses;
			if (requiredQuestStatuses != null && requiredQuestStatuses.Count > 0 && val != null)
			{
				foreach (string linkedExcludedStatus in config.LinkedExcludedStatuses)
				{
					if (Enum.TryParse<EQuestStatus>(linkedExcludedStatus, out EQuestStatus result4) && val.Status == result4)
					{
						LogHelper.LogDebug($"[CONDITION] Linked excluded status matched: {result4}");
						return false;
					}
				}
			}
		}
		if (!string.IsNullOrEmpty(config.RequiredBossSpawned) && !CheckBossSpawned(config.RequiredBossSpawned))
		{
			LogHelper.LogDebug("[CONDITION] Required boss not spawned: " + config.RequiredBossSpawned);
			return false;
		}
		return true;
	}

	private bool CheckBossSpawned(string bossName)
	{
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			GameWorld instance = Singleton<GameWorld>.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				LogHelper.LogDebug("[BOSS] GameWorld instance not found");
				return false;
			}
			foreach (Player allAlivePlayers in instance.AllAlivePlayersList)
			{
				if (allAlivePlayers.IsYourPlayer)
				{
					continue;
				}
				IAIData aIData = allAlivePlayers.AIData;
				if (aIData == null)
				{
					continue;
				}
				BotOwner botOwner = aIData.BotOwner;
				if (botOwner == null)
				{
					continue;
				}
				Profile profile = botOwner.Profile;
				if (profile == null)
				{
					continue;
				}
				ProfileInfo info = profile.Info;
				if (info == null)
				{
					continue;
				}
				ProfileSettings settings = info.Settings;
				if (settings != null)
				{
					_ = settings.Role;
					if (0 == 0 && ((object)Unsafe.As<WildSpawnType, WildSpawnType>(ref allAlivePlayers.AIData.BotOwner.Profile.Info.Settings.Role)/*cast due to constrained. prefix*/).ToString().Equals(bossName, StringComparison.OrdinalIgnoreCase))
					{
						LogHelper.LogDebug($"[BOSS] Found {bossName} at {allAlivePlayers.Transform.position}");
						return true;
					}
				}
			}
			LogHelper.LogDebug("[BOSS] " + bossName + " not found in raid");
			return false;
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"[BOSS] Detection failed: {arg}");
			return false;
		}
	}

	public async Task InitializeBundlesAsync()
	{
		try
		{
			if (BundleManager.Bundles.Keys.Count == 0)
			{
				LogHelper.LogDebug("[AssetLoader] Downloading bundle manifest...");
				await BundleManager.DownloadManifest();
				LogHelper.LogDebug($"[AssetLoader] Manifest loaded with {BundleManager.Bundles.Count} bundles available");
			}
			_bundleKeyLookup.Clear();
			foreach (string key in BundleManager.Bundles.Keys)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(key);
				_bundleKeyLookup.TryAdd(fileNameWithoutExtension, key);
				LogHelper.LogDebug("[AssetLoader] Registered bundle: " + fileNameWithoutExtension + " → " + key);
			}
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"[AssetLoader] Error downloading bundle manifest: {arg}");
		}
	}

	public GameObject LoadPrefabFromBundle(string bundleIdentifier, string assetName)
	{
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			string text = bundleIdentifier;
			if (!BundleManager.Bundles.ContainsKey(text))
			{
				if (!_bundleKeyLookup.TryGetValue(bundleIdentifier, out string value))
				{
					logger.LogError((object)("[AssetLoader] Bundle '" + bundleIdentifier + "' not found in manifest (tried as short name and full key)"));
					return null;
				}
				text = value;
			}
			if (!BundleManager.Bundles.TryGetValue(text, out var value2))
			{
				logger.LogError((object)("[AssetLoader] Bundle key '" + text + "' not in BundleManager"));
				return null;
			}
			string bundleFilePath = BundleManager.GetBundleFilePath(value2);
			if (!File.Exists(bundleFilePath))
			{
				logger.LogError((object)("[AssetLoader] Bundle file not found at: " + bundleFilePath));
				return null;
			}
			if (!_loadedBundles.TryGetValue(text, out AssetBundle value3))
			{
				value3 = AssetBundle.LoadFromFile(bundleFilePath);
				if ((Object)(object)value3 == (Object)null)
				{
					logger.LogError((object)("[AssetLoader] Failed to load bundle from: " + bundleFilePath));
					return null;
				}
				_loadedBundles[text] = value3;
				LogHelper.LogDebug("[AssetLoader] Loaded bundle: " + text + " from " + bundleFilePath);
			}
			GameObject val = value3.LoadAsset<GameObject>(assetName);
			if ((Object)(object)val == (Object)null)
			{
				logger.LogError((object)("[AssetLoader] Prefab '" + assetName + "' not found in bundle '" + text + "'"));
				return null;
			}
			return val;
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"[AssetLoader] Error loading prefab from bundle: {arg}");
			return null;
		}
	}

	private void SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Object.Instantiate<GameObject>(prefab, position, rotation);
			LogHelper.LogDebug($"[Spawner] Instantiated {((Object)prefab).name} at {position}");
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"[Spawner] Failed to instantiate prefab: {arg}");
		}
	}

	public void UnloadAllBundles()
	{
		if (_loadedBundles.Count == 0)
		{
			return;
		}
		foreach (AssetBundle value in _loadedBundles.Values)
		{
			try
			{
				value.Unload(true);
			}
			catch (Exception arg)
			{
				logger.LogError((object)$"[AssetLoader] Error unloading bundle: {arg}");
			}
		}
		_loadedBundles.Clear();
		LogHelper.LogDebug("[AssetLoader] All bundles unloaded");
	}
}
