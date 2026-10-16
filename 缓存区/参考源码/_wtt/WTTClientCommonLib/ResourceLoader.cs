using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BepInEx.Logging;
using EFT;
using EFT.UI.DragAndDrop;
using SPT.Custom.Utils;
using UnityEngine;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib;

public class ResourceLoader(ManualLogSource logger, AssetLoader assetLoader)
{
	public static Dictionary<string, Sprite> _customHideoutIcons;

	public static bool _iconsLoaded;

	public static Dictionary<string, Texture2D> _customMarkTextures;

	public static bool _texturesLoaded;

	public static AudioManifest Manifest { get; private set; }

	public static AudioClipCache ClipCache { get; private set; }

	static ResourceLoader()
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		_customHideoutIcons = new Dictionary<string, Sprite>();
		_iconsLoaded = false;
		_customMarkTextures = new Dictionary<string, Texture2D>();
		_texturesLoaded = false;
		Manifest = new AudioManifest();
		LogHelper.LogDebug("ResourceLoader static constructor: Creating AudioClipCache");
		GameObject val = new GameObject("WTT_AudioClipCache");
		Object.DontDestroyOnLoad((Object)val);
		ClipCache = val.AddComponent<AudioClipCache>();
		((Object)ClipCache).hideFlags = (HideFlags)32;
		LogHelper.LogDebug("AudioClipCache created and set to DontDestroyOnLoad");
	}

	public async void LoadAllResourcesFromServer()
	{
		try
		{
			LogHelper.LogDebug("Loading resources from server...");
			await assetLoader.InitializeBundlesAsync();
			LoadVoicesFromServer();
			LoadSlotImagesFromServer();
			LoadRigLayoutsFromServer();
			LoadAudioManifestFromServer();
			LoadCustomHideoutIconsFromServer();
			LoadCustomMarkTexturesFromServer();
			assetLoader.SpawnConfigs = assetLoader.FetchSpawnConfigs();
			LogHelper.LogDebug($"Loaded {assetLoader.SpawnConfigs.Count} spawn configurations");
			LogHelper.LogDebug("All resources loaded successfully from server");
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error loading resources from server: {arg}");
		}
	}

	public async Task RegisterAudioBundlesAsync(List<string> audioBundleKeys)
	{
		try
		{
			LogHelper.LogDebug($"[AudioManager] Registering {audioBundleKeys.Count} audio bundles");
			if (BundleManager.Bundles.Keys.Count == 0)
			{
				await BundleManager.DownloadManifest();
			}
			foreach (string audioBundleKey in audioBundleKeys)
			{
				if (!BundleManager.Bundles.TryGetValue(audioBundleKey, out var value))
				{
					LogHelper.LogWarn("[AudioManager] Audio bundle '" + audioBundleKey + "' not found in manifest");
					continue;
				}
				string bundleFilePath = BundleManager.GetBundleFilePath(value);
				if (!File.Exists(bundleFilePath))
				{
					LogHelper.LogWarn("[AudioManager] Bundle file not found: " + bundleFilePath);
					continue;
				}
				try
				{
					AssetBundle val = AssetBundle.LoadFromFile(bundleFilePath);
					if ((Object)(object)val == (Object)null)
					{
						LogHelper.LogWarn("[AudioManager] Failed to load bundle: " + audioBundleKey);
						continue;
					}
					AudioClip[] array = val.LoadAllAssets<AudioClip>();
					AudioClip[] array2 = array;
					foreach (AudioClip val2 in array2)
					{
						val2.LoadAudioData();
						ClipCache.CacheAudioClip(((Object)val2).name, val2);
						LogHelper.LogDebug("[AudioManager] Cached audio: " + ((Object)val2).name);
					}
					LogHelper.LogDebug($"[AudioManager] Loaded {array.Length} audio clips from {audioBundleKey}");
				}
				catch (Exception arg)
				{
					LogHelper.LogError($"[AudioManager] Error loading audio bundle {audioBundleKey}: {arg}");
				}
			}
			LogHelper.LogDebug("[AudioManager] Audio bundle registration complete");
		}
		catch (Exception arg2)
		{
			LogHelper.LogError($"[AudioManager] Error registering audio bundles: {arg2}");
		}
	}

	public async Task LoadAudioManifestFromServer()
	{
		try
		{
			LogHelper.LogDebug("[AudioManager] Loading audio manifest from server...");
			AudioManifest audioManifest = Utils.Get<AudioManifest>("/wttcommonlib/audio/manifest/get");
			if (audioManifest == null)
			{
				LogHelper.LogWarn("[AudioManager] No audio manifest received from server");
				return;
			}
			Manifest = audioManifest;
			LogHelper.LogDebug($"[AudioManager] Loaded manifest with {Manifest.FaceCardMappings.Count} face entries");
			LogHelper.LogDebug($"[AudioManager] Radio audio: {Manifest.RadioAudio.Count} tracks");
			List<string> audioBundles = audioManifest.AudioBundles;
			await RegisterAudioBundlesAsync(audioBundles);
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"[AudioManager] Error loading audio manifest: {arg}");
		}
	}

	public static string GetAudioForFace(string faceName)
	{
		LogHelper.LogDebug("[AudioManager] GetAudioForFace called with: " + faceName);
		AudioManifest manifest = Manifest;
		if (manifest != null && manifest.FaceCardMappings.TryGetValue(faceName, out FaceCardAudioEntry value))
		{
			List<string> audio = value.Audio;
			if (audio != null && audio.Count > 0)
			{
				int index = Random.Range(0, value.Audio.Count);
				string text = value.Audio[index];
				LogHelper.LogDebug("[AudioManager] Selected audio key for " + faceName + ": " + text);
				return text;
			}
		}
		LogHelper.LogDebug("[AudioManager] No audio found for face: " + faceName);
		return null;
	}

	public static List<string> GetRadioAudio()
	{
		LogHelper.LogDebug($"[AudioManager] Returning {Manifest?.RadioAudio.Count ?? 0} radio tracks");
		return Manifest?.RadioAudio ?? new List<string>();
	}

	private void LoadVoicesFromServer()
	{
		try
		{
			Dictionary<string, string> dictionary = Utils.Get<Dictionary<string, string>>("/wttcommonlib/voices/get");
			if (dictionary == null)
			{
				logger.LogWarning((object)"No voice data received from server");
				return;
			}
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				if (!InGameBundles._phrasesPaths.ContainsKey(item.Key))
				{
					InGameBundles._phrasesPaths[item.Key] = item.Value;
					LogHelper.LogDebug("Added voice key: " + item.Key);
				}
			}
			LogHelper.LogDebug($"Loaded {dictionary.Count} voice mappings from server");
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error loading voices: {arg}");
		}
	}

	private void LoadSlotImagesFromServer()
	{
		try
		{
			Dictionary<string, string> dictionary = Utils.Get<Dictionary<string, string>>("/wttcommonlib/slotimages/get");
			if (dictionary == null)
			{
				logger.LogWarning((object)"No slot images");
				return;
			}
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				byte[] data;
				try
				{
					data = Convert.FromBase64String(item.Value);
				}
				catch
				{
					logger.LogWarning((object)("Invalid data for " + item.Key));
					continue;
				}
				CreateAndRegisterSlotImage(data, item.Key);
			}
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error loading slot images: {arg}");
		}
	}

	public static async Task LoadCustomMarkTexturesFromServer()
	{
		_ = 1;
		try
		{
			LogHelper.LogDebug("[MarkTextures] Loading custom mark textures from server...");
			await ImageManager.DownloadMarkTexturesManifest();
			if (ImageManager.ShootingRangeMarks.Count == 0)
			{
				LogHelper.LogWarn("[MarkTextures] No textures in manifest");
				return;
			}
			LogHelper.LogDebug($"[MarkTextures] Manifest contains {ImageManager.ShootingRangeMarks.Count} textures");
			int loadedCount = 0;
			foreach (var (fileName, imageItem2) in ImageManager.ShootingRangeMarks)
			{
				LogHelper.LogDebug("[MarkTextures] Processing texture: " + fileName + ", ModPath: " + imageItem2.ModPath);
				try
				{
					Texture2D val = await ImageManager.LoadMarkTexture(fileName);
					if ((Object)(object)val != (Object)null)
					{
						string key = (((Object)val).name = Path.GetFileNameWithoutExtension(fileName));
						_customMarkTextures[key] = val;
						loadedCount++;
						LogHelper.LogDebug("[MarkTextures] Loaded icon for item: " + fileName);
					}
					else
					{
						LogHelper.LogWarn("[MarkTextures] LoadImage returned null for: " + fileName);
					}
				}
				catch (Exception ex)
				{
					LogHelper.LogError("[MarkTextures] Error loading texture " + fileName + ": " + ex.Message);
				}
			}
			LogHelper.LogDebug($"[MarkTextures] Custom hideout icons loaded successfully ({loadedCount} total)");
			_iconsLoaded = true;
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"[MarkTextures] Error loading custom hideout icons: {arg}");
		}
	}

	public static async Task LoadCustomHideoutIconsFromServer()
	{
		_ = 1;
		try
		{
			LogHelper.LogDebug("[HideoutIcons] Loading custom hideout customization icons from server...");
			await ImageManager.DownloadManifest();
			if (ImageManager.HideoutIcons.Count == 0)
			{
				LogHelper.LogWarn("[HideoutIcons] No icons in manifest");
				return;
			}
			LogHelper.LogDebug($"[HideoutIcons] Manifest contains {ImageManager.HideoutIcons.Count} icons");
			int loadedCount = 0;
			foreach (var (fileName, imageItem2) in ImageManager.HideoutIcons)
			{
				LogHelper.LogDebug("[HideoutIcons] Processing icon: " + fileName + ", ModPath: " + imageItem2.ModPath);
				try
				{
					Texture2D val = await ImageManager.LoadImage(fileName);
					if ((Object)(object)val != (Object)null)
					{
						Sprite val2 = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f));
						string text2 = (((Object)val2).name = Path.GetFileNameWithoutExtension(fileName));
						_customHideoutIcons[text2] = val2;
						loadedCount++;
						LogHelper.LogDebug("[HideoutIcons] Loaded icon for item: " + text2);
					}
					else
					{
						LogHelper.LogWarn("[HideoutIcons] LoadImage returned null for: " + fileName);
					}
				}
				catch (Exception ex)
				{
					LogHelper.LogError("[HideoutIcons] Error loading icon " + fileName + ": " + ex.Message);
				}
			}
			LogHelper.LogDebug($"[HideoutIcons] Custom hideout icons loaded successfully ({loadedCount} total)");
			_iconsLoaded = true;
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"[HideoutIcons] Error loading custom hideout icons: {arg}");
		}
	}

	private void LoadRigLayoutsFromServer()
	{
		try
		{
			Dictionary<string, string> dictionary = Utils.Get<Dictionary<string, string>>("/wttcommonlib/riglayouts/get");
			if (dictionary == null)
			{
				logger.LogWarning((object)"No rig layouts received from server");
				return;
			}
			LogHelper.LogDebug($"Received {dictionary.Count} rig layouts from server");
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				string key = item.Key;
				string value = item.Value;
				if (string.IsNullOrEmpty(value))
				{
					logger.LogWarning((object)("No data for rig layout: " + key));
					continue;
				}
				byte[] array;
				try
				{
					array = Convert.FromBase64String(value);
				}
				catch (Exception arg)
				{
					logger.LogError((object)$"Base64 decode failed for rig layout {key}: {arg}");
					continue;
				}
				if (array.Length == 0)
				{
					logger.LogWarning((object)("Bundle data is empty for rig layout: " + key));
				}
				else
				{
					LoadBundleFromMemory(array, key);
				}
			}
			LogHelper.LogDebug($"Loaded {dictionary.Count} rig layouts from server");
		}
		catch (Exception arg2)
		{
			logger.LogError((object)$"Error loading rig layouts: {arg2}");
		}
	}

	private void CreateAndRegisterSlotImage(byte[] data, string slotID)
	{
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (data == null || data.Length == 0)
			{
				logger.LogWarning((object)("Empty data for slot image: " + slotID));
				return;
			}
			Texture2D val = new Texture2D(2, 2);
			if (!ImageConversion.LoadImage(val, data))
			{
				logger.LogWarning((object)("Failed to create texture for " + slotID));
				return;
			}
			Sprite val2 = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f), 100f);
			val2.texture.ignoreMipmapLimit = true;
			((Texture)val2.texture).mipMapBias = -1f;
			ResourceHelper.AddEntry("Slots/" + slotID, val2);
			LogHelper.LogDebug("Added slot sprite: " + slotID);
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error creating slot sprite {slotID}: {arg}");
		}
	}

	private void LoadBundleFromMemory(byte[] data, string bundleName)
	{
		try
		{
			if (data == null || data.Length == 0)
			{
				logger.LogWarning((object)("Bundle data is null or empty for: " + bundleName));
				return;
			}
			AssetBundle val = AssetBundle.LoadFromMemory(data);
			if ((Object)(object)val == (Object)null)
			{
				logger.LogWarning((object)("Failed to load rig layout bundle: " + bundleName));
				return;
			}
			int num = 0;
			GameObject[] array = val.LoadAllAssets<GameObject>();
			if (array == null || array.Length == 0)
			{
				logger.LogWarning((object)("No GameObjects loaded from bundle: " + bundleName));
			}
			if (array != null)
			{
				GameObject[] array2 = array;
				foreach (GameObject val2 in array2)
				{
					if ((Object)(object)val2 == (Object)null)
					{
						logger.LogWarning((object)"Encountered null prefab in bundle.");
						continue;
					}
					ContainedGridsView component = val2.GetComponent<ContainedGridsView>();
					if ((Object)(object)component == (Object)null)
					{
						logger.LogWarning((object)("Prefab " + ((Object)val2).name + " missing ContainedGridsView."));
						continue;
					}
					ResourceHelper.AddEntry("UI/Rig Layouts/" + ((Object)val2).name, component);
					num++;
					LogHelper.LogDebug("Added rig layout: " + ((Object)val2).name);
				}
			}
			val.Unload(false);
			LogHelper.LogDebug($"Loaded {num} prefabs from bundle: {bundleName}");
		}
		catch (Exception arg)
		{
			logger.LogError((object)$"Error loading bundle {bundleName}: {arg}");
		}
	}
}
