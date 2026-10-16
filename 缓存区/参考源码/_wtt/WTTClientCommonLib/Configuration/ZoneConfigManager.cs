using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WTTClientCommonLib.Attributes;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Configuration;

public static class ZoneConfigManager
{
	internal static Color ColorZoneRed;

	internal static Color ColorZoneGreen;

	public static List<CustomQuestZone> ExistingQuestZones { get; set; }

	public static ConfigEntry<string> NewZoneName { get; private set; }

	public static ConfigEntry<string> NewZoneType { get; private set; }

	public static ConfigEntry<string> FlareZoneType { get; private set; }

	public static ConfigEntry<float> ZoneAdjustmentValue { get; private set; }

	public static ConfigEntry<float> PositionConfigX { get; private set; }

	public static ConfigEntry<float> PositionConfigY { get; private set; }

	public static ConfigEntry<float> PositionConfigZ { get; private set; }

	public static ConfigEntry<float> ScaleConfigX { get; private set; }

	public static ConfigEntry<float> ScaleConfigY { get; private set; }

	public static ConfigEntry<float> ScaleConfigZ { get; private set; }

	public static ConfigEntry<float> RotationConfigX { get; private set; }

	public static ConfigEntry<float> RotationConfigY { get; private set; }

	public static ConfigEntry<float> RotationConfigZ { get; private set; }

	public static void Initialize(ConfigFile config)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Expected O, but got Unknown
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Expected O, but got Unknown
		//IL_0123: Unknown result type (might be due to invalid IL or missing references)
		//IL_012d: Expected O, but got Unknown
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0189: Expected O, but got Unknown
		//IL_01a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01af: Expected O, but got Unknown
		//IL_0205: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Expected O, but got Unknown
		//IL_0265: Unknown result type (might be due to invalid IL or missing references)
		//IL_026f: Expected O, but got Unknown
		//IL_02c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cf: Expected O, but got Unknown
		//IL_0325: Unknown result type (might be due to invalid IL or missing references)
		//IL_032f: Expected O, but got Unknown
		//IL_0385: Unknown result type (might be due to invalid IL or missing references)
		//IL_038f: Expected O, but got Unknown
		//IL_03e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Expected O, but got Unknown
		//IL_0445: Unknown result type (might be due to invalid IL or missing references)
		//IL_044f: Expected O, but got Unknown
		//IL_04a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_04af: Expected O, but got Unknown
		//IL_0505: Unknown result type (might be due to invalid IL or missing references)
		//IL_050f: Expected O, but got Unknown
		//IL_0559: Unknown result type (might be due to invalid IL or missing references)
		//IL_0563: Expected O, but got Unknown
		//IL_05a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_05af: Expected O, but got Unknown
		if (UniversalConfigManager.DeveloperMode.Value)
		{
			NewZoneName = config.Bind<string>("1. Create CustomQuestZone", "CustomQuestZone ID", "", new ConfigDescription("The name for the new zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					Order = 4
				}
			}));
			NewZoneType = config.Bind<string>("1. Create CustomQuestZone", "CustomQuestZone Type", "", new ConfigDescription("Select the type of zone", (AcceptableValueBase)(object)ZoneUiHelpers.AcceptableTypes, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					Order = 3
				}
			}));
			FlareZoneType = config.Bind<string>("1. Create CustomQuestZone", "(Optional) Flare Type", "", new ConfigDescription("Select the flare zone type", (AcceptableValueBase)(object)ZoneUiHelpers.AcceptableFlareTypes, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					Order = 2
				}
			}));
			config.Bind<string>("1. Create CustomQuestZone", "Add CustomQuestZone", "New CustomQuestZone", new ConfigDescription("Adds a new zone with the zone ID", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.NewZoneDrawer,
					Order = 1
				}
			}));
			config.Bind<string>("2. Select CustomQuestZone", "Navigate Zones", "", new ConfigDescription("The ID for the currently selected CustomQuestZone.", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.SwitchZoneDrawer,
					ReadOnly = true
				}
			}));
			ZoneAdjustmentValue = config.Bind<float>("3.1. Adjustment Settings", "CustomQuestZone Adjustment Value", 0.25f, new ConfigDescription("Sets the value used to adjust the position, scale and rotation of zones.", (AcceptableValueBase)null, Array.Empty<object>()));
			PositionConfigX = config.Bind<float>("3.2. Change Position", "Change Position X", 0f, new ConfigDescription("Change the position of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.PositionXDrawer,
					ReadOnly = true
				}
			}));
			PositionConfigY = config.Bind<float>("3.2. Change Position", "Change Position Y", 0f, new ConfigDescription("Change the position of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.PositionYDrawer,
					ReadOnly = true
				}
			}));
			PositionConfigZ = config.Bind<float>("3.2. Change Position", "Change Position Z", 0f, new ConfigDescription("Change the position of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.PositionZDrawer,
					ReadOnly = true
				}
			}));
			ScaleConfigX = config.Bind<float>("3.3. Change Scale", "Change Scale X", 0f, new ConfigDescription("Change the scale of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.ScaleXDrawer,
					ReadOnly = true
				}
			}));
			ScaleConfigY = config.Bind<float>("3.3. Change Scale", "Change Scale Y", 0f, new ConfigDescription("Change the scale of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.ScaleYDrawer,
					ReadOnly = true
				}
			}));
			ScaleConfigZ = config.Bind<float>("3.3. Change Scale", "Change Scale Z", 0f, new ConfigDescription("Change the scale of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.ScaleZDrawer,
					ReadOnly = true
				}
			}));
			RotationConfigX = config.Bind<float>("3.4. Change Rotation", "Change Rotation X", 0f, new ConfigDescription("Change the rotation of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.RotationXDrawer,
					ReadOnly = true
				}
			}));
			RotationConfigY = config.Bind<float>("3.4. Change Rotation", "Change Rotation Y", 0f, new ConfigDescription("Change the rotation of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.RotationYDrawer,
					ReadOnly = true
				}
			}));
			RotationConfigZ = config.Bind<float>("3.4. Change Rotation", "Change Rotation Z", 0f, new ConfigDescription("Change the rotation of the current zone", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.RotationZDrawer,
					ReadOnly = true
				}
			}));
			config.Bind<string>("4. Output", "Output Zones", "", new ConfigDescription("Outputs all zones to the root SPT directory.", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.OutputDrawer
				}
			}));
			config.Bind<bool>("5. View Zones", "Add Existing Zones", false, new ConfigDescription("Adds any currently loaded custom zones to the editor.", (AcceptableValueBase)null, new object[1]
			{
				new ConfigurationManagerAttributes
				{
					CustomDrawer = ZoneUiHelpers.ViewZonesDrawer
				}
			}));
			NewZoneName.Value = (string)((ConfigEntryBase)NewZoneName).DefaultValue;
			NewZoneType.Value = (string)((ConfigEntryBase)NewZoneType).DefaultValue;
			FlareZoneType.Value = (string)((ConfigEntryBase)FlareZoneType).DefaultValue;
			PositionConfigX.Value = (float)((ConfigEntryBase)PositionConfigX).DefaultValue;
			PositionConfigY.Value = (float)((ConfigEntryBase)PositionConfigY).DefaultValue;
			PositionConfigZ.Value = (float)((ConfigEntryBase)PositionConfigZ).DefaultValue;
			ScaleConfigX.Value = (float)((ConfigEntryBase)ScaleConfigX).DefaultValue;
			ScaleConfigY.Value = (float)((ConfigEntryBase)ScaleConfigY).DefaultValue;
			ScaleConfigZ.Value = (float)((ConfigEntryBase)ScaleConfigZ).DefaultValue;
			RotationConfigX.Value = (float)((ConfigEntryBase)RotationConfigX).DefaultValue;
			RotationConfigY.Value = (float)((ConfigEntryBase)RotationConfigY).DefaultValue;
			RotationConfigZ.Value = (float)((ConfigEntryBase)RotationConfigZ).DefaultValue;
		}
	}

	static ZoneConfigManager()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		ColorZoneRed = new Color(1f, 0f, 0f, 0.7f);
		ColorZoneGreen = new Color(0f, 1f, 0f, 0.7f);
		ExistingQuestZones = new List<CustomQuestZone>();
	}
}
