using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using WTTClientCommonLib.Helpers;

namespace WTTClientCommonLib.Services;

public static class QuestIcons
{
	private static Sprite _salvageSprite;

	private static bool _initialized;

	private static bool _failed;

	public static Sprite SalvageSprite
	{
		get
		{
			if (!_initialized && !_failed)
			{
				Init();
			}
			return _salvageSprite;
		}
	}

	public static void Init()
	{
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		if (_initialized || _failed)
		{
			return;
		}
		try
		{
			string text = Path.Combine(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "QuestIcons"), "icon_salvage.png");
			if (!File.Exists(text))
			{
				LogHelper.LogError("Salvage quest icon not found at " + text);
				_failed = true;
				return;
			}
			byte[] array = File.ReadAllBytes(text);
			if (array == null || array.Length == 0)
			{
				LogHelper.LogError("Salvage quest icon file is empty: " + text);
				_failed = true;
				return;
			}
			Texture2D val = new Texture2D(2, 2, (TextureFormat)4, true);
			if (!ImageConversion.LoadImage(val, array))
			{
				LogHelper.LogError("Failed to load texture for salvage quest icon: " + text);
				Object.Destroy((Object)(object)val);
				_failed = true;
			}
			else
			{
				_salvageSprite = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f), 100f);
				_salvageSprite.texture.ignoreMipmapLimit = true;
				((Texture)_salvageSprite.texture).mipMapBias = -1f;
				LogHelper.LogDebug("Loaded salvage quest icon from " + text);
				_initialized = true;
			}
		}
		catch (Exception arg)
		{
			LogHelper.LogError($"Error loading salvage quest icon: {arg}");
			_failed = true;
		}
	}
}
