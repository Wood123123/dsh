using System;
using BepInEx.Configuration;
using UnityEngine;
using WTTClientCommonLib.Attributes;
using WTTClientCommonLib.Components;

namespace WTTClientCommonLib.Configuration;

internal static class RadioSettings
{
	public static ConfigEntry<float> FaceCardVolume;

	public static ConfigEntry<bool> GymEnabled;

	public static ConfigEntry<float> GymReplacementChance;

	public static ConfigEntry<bool> GymPlayOnFirstEntrance;

	public static ConfigEntry<float> GymRadioVolume;

	public static ConfigEntry<bool> RestSpaceEnabled;

	public static ConfigEntry<float> RestSpaceReplacementChance;

	public static ConfigEntry<bool> RestSpacePlayOnFirstEntrance;

	public static ConfigEntry<float> RestSpaceRadioVolume;

	public static void Init(ConfigFile config)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Expected O, but got Unknown
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Expected O, but got Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Expected O, but got Unknown
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0194: Expected O, but got Unknown
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e1: Expected O, but got Unknown
		//IL_0224: Unknown result type (might be due to invalid IL or missing references)
		//IL_022e: Expected O, but got Unknown
		//IL_025f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0269: Expected O, but got Unknown
		FaceCardVolume = config.Bind<float>("FaceCard", "Music Volume", 0.06f, new ConfigDescription("Volume for the FaceCard custom audio", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 1f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 9
			}
		}));
		GymEnabled = config.Bind<bool>("Gym Radio", "Enabled", true, new ConfigDescription("Enable/disable custom audio for the Gym radio", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 8
			}
		}));
		GymRadioVolume = config.Bind<float>("Gym Radio", "Gym Radio Volume", 0.6f, new ConfigDescription("Volume for the Gym Radio custom audio)", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 1f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 7
			}
		}));
		GymReplacementChance = config.Bind<float>("Gym Radio", "Replacement Chance", 10f, new ConfigDescription("Percentage chance to replace gym radio audio (0-100)", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 100f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 6
			}
		}));
		GymPlayOnFirstEntrance = config.Bind<bool>("Gym Radio", "Play On First Entrance", true, new ConfigDescription("Play custom audio when first entering hideout", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 5
			}
		}));
		RestSpaceEnabled = config.Bind<bool>("Rest Space Radio", "Enabled", true, new ConfigDescription("Enable/disable custom audio for the Rest Space radio", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 4
			}
		}));
		RestSpaceRadioVolume = config.Bind<float>("Rest Space Radio", "Rest Space Radio Volume", 0.6f, new ConfigDescription("Volume for the Rest Space Radio custom audio)", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 1f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 3
			}
		}));
		RestSpaceReplacementChance = config.Bind<float>("Rest Space Radio", "Replacement Chance", 10f, new ConfigDescription("Percentage chance to replace rest space radio audio (0-100)", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 100f), new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 2
			}
		}));
		RestSpacePlayOnFirstEntrance = config.Bind<bool>("Rest Space Radio", "Play On First Entrance", true, new ConfigDescription("Play custom audio when first entering hideout", (AcceptableValueBase)null, new object[1]
		{
			new ConfigurationManagerAttributes
			{
				Order = 1
			}
		}));
		FaceCardVolume.SettingChanged += ConfigSettingChanged;
		GymEnabled.SettingChanged += ConfigSettingChanged;
		GymRadioVolume.SettingChanged += ConfigSettingChanged;
		GymReplacementChance.SettingChanged += ConfigSettingChanged;
		GymPlayOnFirstEntrance.SettingChanged += ConfigSettingChanged;
		RestSpaceEnabled.SettingChanged += ConfigSettingChanged;
		RestSpaceRadioVolume.SettingChanged += ConfigSettingChanged;
		RestSpaceReplacementChance.SettingChanged += ConfigSettingChanged;
		RestSpacePlayOnFirstEntrance.SettingChanged += ConfigSettingChanged;
	}

	private static void ConfigSettingChanged(object sender, EventArgs e)
	{
		RefreshAllTrackers();
	}

	private static void RefreshAllTrackers()
	{
		HideoutRadioStateTracker[] array = Object.FindObjectsOfType<HideoutRadioStateTracker>();
		for (int i = 0; i < array.Length; i++)
		{
			array[i].RefreshSettings();
		}
	}
}
