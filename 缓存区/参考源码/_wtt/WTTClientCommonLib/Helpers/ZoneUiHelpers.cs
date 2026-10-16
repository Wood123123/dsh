using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Models;
using WTTClientCommonLib.Services;

namespace WTTClientCommonLib.Helpers;

public static class ZoneUiHelpers
{
	public static readonly AcceptableValueList<string> AcceptableTypes = new AcceptableValueList<string>(new string[5] { "placeitem", "visit", "flarezone", "botkillzone", "salvage" });

	public static readonly AcceptableValueList<string> AcceptableFlareTypes = new AcceptableValueList<string>(new string[6] { "", "Light", "Airdrop", "ExitActivate", "Quest", "AIFollowEvent" });

	public static List<CustomQuestZone> ExistingQuestZones = new List<CustomQuestZone>();

	public static void ViewZonesDrawer(ConfigEntryBase entry)
	{
		if (GUILayout.Button("Add Existing Zones", Array.Empty<GUILayoutOption>()))
		{
			ZoneService.AddExistingZones();
		}
	}

	public static void NewZoneDrawer(ConfigEntryBase entry)
	{
		if (GUILayout.Button("New CustomQuestZone", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(100f) }))
		{
			ZoneService.CreateNewZone();
		}
	}

	public static void SwitchZoneDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Prev", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ZoneService.PrevZone();
		}
		GUILayout.TextField(ZoneService.GetCurrentZoneName(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("Next", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ZoneService.NextZone();
		}
		GUILayout.EndHorizontal();
	}

	public static void PositionXDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigX = ZoneConfigManager.PositionConfigX;
			positionConfigX.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.TextField(ZoneConfigManager.PositionConfigX.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigX2 = ZoneConfigManager.PositionConfigX;
			positionConfigX2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.EndHorizontal();
	}

	public static void PositionYDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigY = ZoneConfigManager.PositionConfigY;
			positionConfigY.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.TextField(ZoneConfigManager.PositionConfigY.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigY2 = ZoneConfigManager.PositionConfigY;
			positionConfigY2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.EndHorizontal();
	}

	public static void PositionZDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigZ = ZoneConfigManager.PositionConfigZ;
			positionConfigZ.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.TextField(ZoneConfigManager.PositionConfigZ.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> positionConfigZ2 = ZoneConfigManager.PositionConfigZ;
			positionConfigZ2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustPosition();
		}
		GUILayout.EndHorizontal();
	}

	public static void ScaleXDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigX = ZoneConfigManager.ScaleConfigX;
			scaleConfigX.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.TextField(ZoneConfigManager.ScaleConfigX.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigX2 = ZoneConfigManager.ScaleConfigX;
			scaleConfigX2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.EndHorizontal();
	}

	public static void ScaleYDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigY = ZoneConfigManager.ScaleConfigY;
			scaleConfigY.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.TextField(ZoneConfigManager.ScaleConfigY.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigY2 = ZoneConfigManager.ScaleConfigY;
			scaleConfigY2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.EndHorizontal();
	}

	public static void ScaleZDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigZ = ZoneConfigManager.ScaleConfigZ;
			scaleConfigZ.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.TextField(ZoneConfigManager.ScaleConfigZ.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> scaleConfigZ2 = ZoneConfigManager.ScaleConfigZ;
			scaleConfigZ2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustScale();
		}
		GUILayout.EndHorizontal();
	}

	public static void RotationXDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigX = ZoneConfigManager.RotationConfigX;
			rotationConfigX.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.TextField(ZoneConfigManager.RotationConfigX.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigX2 = ZoneConfigManager.RotationConfigX;
			rotationConfigX2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.EndHorizontal();
	}

	public static void RotationYDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigY = ZoneConfigManager.RotationConfigY;
			rotationConfigY.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.TextField(ZoneConfigManager.RotationConfigY.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigY2 = ZoneConfigManager.RotationConfigY;
			rotationConfigY2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.EndHorizontal();
	}

	public static void RotationZDrawer(ConfigEntryBase entry)
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("<--", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigZ = ZoneConfigManager.RotationConfigZ;
			rotationConfigZ.Value -= ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.TextField(ZoneConfigManager.RotationConfigZ.Value.ToString(), (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
		if (GUILayout.Button("-->", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(45f) }))
		{
			ConfigEntry<float> rotationConfigZ2 = ZoneConfigManager.RotationConfigZ;
			rotationConfigZ2.Value += ZoneConfigManager.ZoneAdjustmentValue.Value;
			ZoneService.AdjustRotation();
		}
		GUILayout.EndHorizontal();
	}

	public static void OutputDrawer(ConfigEntryBase entry)
	{
		if (GUILayout.Button("Output Zones", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) }))
		{
			ZoneService.OutputZones();
		}
	}
}
