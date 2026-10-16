using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using SPT.Common.Http;
using SPT.Common.Utils;
using UnityEngine;
using WTTClientCommonLib;
using WTTClientCommonLib.Models;

namespace SPT.Custom.Utils;

public static class ImageManager
{
	private const string BaseCachePath = "SPT_Runtime/user/cache/";

	private const string HideoutIconsCachePath = "SPT_Runtime/user/cache/hideouticons/";

	private const string ShootingRangeMarksCachePath = "SPT_Runtime/user/cache/shootingrangemarks/";

	private static readonly ManualLogSource LogHelper;

	public static readonly ConcurrentDictionary<string, ImageItem> HideoutIcons;

	public static readonly ConcurrentDictionary<string, ImageItem> ShootingRangeMarks;

	static ImageManager()
	{
		LogHelper = Logger.CreateLogSource("ImageManager");
		HideoutIcons = new ConcurrentDictionary<string, ImageItem>();
		ShootingRangeMarks = new ConcurrentDictionary<string, ImageItem>();
	}

	private static string GetCachePath(ImageType type)
	{
		return type switch
		{
			ImageType.HideoutIcon => "SPT_Runtime/user/cache/hideouticons/", 
			ImageType.ShootingRangeMark => "SPT_Runtime/user/cache/shootingrangemarks/", 
			_ => "SPT_Runtime/user/cache/", 
		};
	}

	private static string GetDownloadRoute(ImageType type)
	{
		return type switch
		{
			ImageType.HideoutIcon => "/files/image", 
			ImageType.ShootingRangeMark => "/files/texture", 
			_ => "/files/image", 
		};
	}

	public static string GetImagePath(ImageItem image, ImageType type)
	{
		if (!RequestHandler.IsLocal)
		{
			return GetCachePath(type);
		}
		return "SPT_Runtime/" + image.ModPath + "/";
	}

	public static string GetImageFilePath(ImageItem image, ImageType type)
	{
		return GetImagePath(image, type) + image.FileName;
	}

	public static async Task DownloadManifest()
	{
		try
		{
			LogHelper.LogDebug((object)"Downloading hideout icons manifest...");
			ImageItem[] array = JsonConvert.DeserializeObject<ImageItem[]>(await RequestHandler.GetJsonAsync("/wttcommonlib/hideout/icons/manifest"));
			if (array != null)
			{
				ImageItem[] array2 = array;
				foreach (ImageItem imageItem in array2)
				{
					HideoutIcons.TryAdd(imageItem.FileName, imageItem);
				}
				LogHelper.LogDebug((object)$"Downloaded manifest for {array.Length} hideout icons");
			}
		}
		catch (Exception ex)
		{
			LogHelper.LogError((object)("Error downloading hideout icons manifest: " + ex.Message));
		}
	}

	public static async Task DownloadMarkTexturesManifest()
	{
		try
		{
			LogHelper.LogDebug((object)"Downloading shooting range marks manifest...");
			ImageItem[] array = JsonConvert.DeserializeObject<ImageItem[]>(await RequestHandler.GetJsonAsync("/wttcommonlib/hideout/marktextures/manifest"));
			if (array != null)
			{
				ImageItem[] array2 = array;
				foreach (ImageItem imageItem in array2)
				{
					ShootingRangeMarks.TryAdd(imageItem.FileName, imageItem);
				}
				LogHelper.LogDebug((object)$"Downloaded manifest for {array.Length} shooting range marks");
			}
		}
		catch (Exception ex)
		{
			LogHelper.LogError((object)("Error downloading shooting range marks manifest: " + ex.Message));
		}
	}

	public static async Task DownloadImage(ImageItem image, ImageType type, Action<DownloadProgress> progressCallback = null)
	{
		_ = 1;
		try
		{
			string cachePath = GetCachePath(type);
			string filepath = cachePath + image.FileName;
			string directoryName = Path.GetDirectoryName(filepath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
				LogHelper.LogDebug((object)("[DownloadImage] Created directory: " + directoryName));
			}
			LogHelper.LogDebug((object)$"[DownloadImage] Downloading {image.FileName} ({type}) to {filepath}");
			string text = await RequestHandler.GetJsonAsync(GetDownloadRoute(type) + "/" + image.FileName);
			if (string.IsNullOrEmpty(text))
			{
				LogHelper.LogError((object)("[DownloadImage] Received empty data for " + image.FileName));
				return;
			}
			LogHelper.LogDebug((object)$"[DownloadImage] Received {text.Length} chars of base64 for {image.FileName}");
			try
			{
				byte[] binaryData = Convert.FromBase64String(text);
				LogHelper.LogDebug((object)$"[DownloadImage] Decoded to {binaryData.Length} bytes, writing to disk...");
				await File.WriteAllBytesAsync(filepath, binaryData);
				LogHelper.LogDebug((object)$"[DownloadImage] Successfully wrote {binaryData.Length} bytes to {filepath}");
				if (File.Exists(filepath))
				{
					LogHelper.LogDebug((object)("[DownloadImage] File verified to exist at " + filepath));
				}
				else
				{
					LogHelper.LogError((object)("[DownloadImage] File write failed - file does not exist at " + filepath));
				}
			}
			catch (FormatException ex)
			{
				LogHelper.LogError((object)("[DownloadImage] Failed to decode base64 for " + image.FileName + ": " + ex.Message));
			}
		}
		catch (Exception ex2)
		{
			LogHelper.LogError((object)("[DownloadImage] Error: " + ex2.Message + "\n" + ex2.StackTrace));
		}
	}

	public static async Task<Texture2D?> LoadImage(string imageName)
	{
		if (!HideoutIcons.TryGetValue(imageName, out ImageItem value))
		{
			LogHelper.LogWarning((object)("Hideout icon not found in manifest: " + imageName));
			return null;
		}
		return await LoadImageInternal(imageName, value, ImageType.HideoutIcon);
	}

	public static async Task<Texture2D?> LoadMarkTexture(string textureName)
	{
		if (!ShootingRangeMarks.TryGetValue(textureName, out ImageItem value))
		{
			LogHelper.LogWarning((object)("Shooting range mark not found in manifest: " + textureName));
			return null;
		}
		return await LoadImageInternal(textureName, value, ImageType.ShootingRangeMark);
	}

	public static async Task<bool> ShouldAcquire(ImageItem image, ImageType type)
	{
		if (await IsCacheValid(global::WTTClientCommonLib.WTTClientCommonLib.FikaInstalled ? Path.Combine(GetCachePath(type), image.FileName) : GetImageFilePath(image, type), image))
		{
			LogHelper.LogDebug((object)("[ShouldAcquire] File is current: " + image.FileName));
			return false;
		}
		LogHelper.LogDebug((object)("[ShouldAcquire] Acquiring: " + image.FileName));
		return true;
	}

	private static async Task<bool> IsCacheValid(string filepath, ImageItem image)
	{
		if (!VFS.Exists(filepath))
		{
			return false;
		}
		uint num = Crc32.CRC32BytesPHP(await VFS.ReadFileAsync(filepath));
		if (num != image.Crc)
		{
			LogHelper.LogDebug((object)("[ImageManager] CRC mismatch for " + image.FileName + ": " + $"expected {image.Crc}, got {num}"));
			return false;
		}
		return true;
	}

	private static async Task<Texture2D?> LoadImageInternal(string imageName, ImageItem image, ImageType type)
	{
		_ = 5;
		try
		{
			bool flag = false;
			bool fikaInstalled = global::WTTClientCommonLib.WTTClientCommonLib.FikaInstalled;
			string cachePath = GetCachePath(type);
			string filepath = ((flag | fikaInstalled) ? (cachePath + image.FileName) : GetImageFilePath(image, type));
			LogHelper.LogDebug((object)$"[LoadImage] TEST_MODE: {flag}, Fika: {fikaInstalled}, Type: {type}, FilePath: {filepath}, IsLocal: {RequestHandler.IsLocal}");
			if (flag)
			{
				if (VFS.Exists(filepath))
				{
					LogHelper.LogDebug((object)("[LoadImage] TEST MODE: File already in cache, using cached version: " + imageName));
				}
				else
				{
					LogHelper.LogDebug((object)("[LoadImage] TEST MODE: File not in cache, downloading " + imageName));
					await DownloadImage(image, type);
				}
			}
			else if (fikaInstalled)
			{
				if (await ShouldAcquire(image, type))
				{
					await DownloadImage(image, type);
				}
			}
			else if (!RequestHandler.IsLocal && await ShouldAcquire(image, type))
			{
				await DownloadImage(image, type);
			}
			if (!VFS.Exists(filepath))
			{
				LogHelper.LogError((object)("[LoadImage] File does not exist: " + filepath));
				return null;
			}
			byte[] array = await VFS.ReadFileAsync(filepath);
			Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
			if (ImageConversion.LoadImage(val, array))
			{
				val.Apply(true, true);
				return val;
			}
			Object.Destroy((Object)(object)val);
			return null;
		}
		catch (Exception ex)
		{
			LogHelper.LogError((object)("Error loading image " + imageName + ": " + ex.Message));
			return null;
		}
	}
}
