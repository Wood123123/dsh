using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace StashTools;

internal static class ChineseSettings
{
	private static object? _manager;

	private static PropertyInfo? _displaying;

	private static int _captureFrame;

	private static readonly KeyCode[] Keys = ((KeyCode[])Enum.GetValues(typeof(KeyCode))).Distinct().Where(delegate(KeyCode k)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		return (int)k != 0 && (int)k != 323;
	}).ToArray();

	internal static bool IsOpen
	{
		get
		{
			if (_manager != null)
			{
				object obj = _displaying?.GetValue(_manager);
				if (obj is bool)
				{
					return (bool)obj;
				}
				return false;
			}
			return false;
		}
	}

	internal static ConfigDescription ToggleDescription(string description)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		return new ConfigDescription(description, (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				CustomDrawer = DrawToggle
			}
		});
	}

	private static void DrawToggle(ConfigEntryBase entry)
	{
		bool flag = (bool)entry.BoxedValue;
		bool flag2 = GUILayout.Toggle(flag, flag ? "已启用" : "已关闭", Array.Empty<GUILayoutOption>());
		if (flag2 != flag)
		{
			entry.BoxedValue = flag2;
		}
	}

	internal static void MigrateConfig(ConfigFile config)
	{
		if (File.Exists(config.ConfigFilePath))
		{
			string text = File.ReadAllText(config.ConfigFilePath);
			string text2 = SettingsText.MigrateConfigText(text);
			if (!(text2 == text))
			{
				File.Copy(config.ConfigFilePath, config.ConfigFilePath + ".pre-0.1.2-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: false);
				File.WriteAllText(config.ConfigFilePath, text2, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				config.Reload();
			}
		}
	}

	internal static void FindManager()
	{
		foreach (PluginInfo value in Chainloader.PluginInfos.Values)
		{
			if (!(((object)value.Instance)?.GetType().FullName != "ConfigurationManager.ConfigurationManager"))
			{
				_manager = value.Instance;
				_displaying = _manager.GetType().GetProperty("DisplayingWindow");
				break;
			}
		}
	}

	internal static void DrawKey(ConfigEntryBase entry, ref bool accepting)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Invalid comparison between Unknown and I4
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		if (accepting && Time.frameCount > _captureFrame + 1)
		{
			KeyCode[] keys = Keys;
			foreach (KeyCode val in keys)
			{
				if (Input.GetKeyUp(val))
				{
					if ((int)val != 27)
					{
						entry.BoxedValue = val;
					}
					accepting = false;
					break;
				}
			}
		}
		if (GUILayout.Button(accepting ? "请按下并松开新键（点击取消）" : (SettingsText.KeyName(((object)(KeyCode)entry.BoxedValue/*cast due to constrained. prefix*/).ToString()) + "（点击修改）"), Array.Empty<GUILayoutOption>()))
		{
			accepting = !accepting;
			_captureFrame = Time.frameCount;
		}
	}
}
