using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;
using WTTClientCommonLib.Configuration;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Services;

public static class ZoneService
{
	private static readonly List<CustomZoneContainer> Zones = new List<CustomZoneContainer>();

	private static int _currentSelectIndex = -1;

	private static string _selectedZoneName = "No CustomQuestZone Selected";

	public static void AddExistingZones()
	{
		ZoneConfigManager.ExistingQuestZones.ForEach(delegate(CustomQuestZone questZone)
		{
			//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
			//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			GameObject val = Utils.CreateNewZoneCube(questZone.ZoneName);
			if (!((Object)(object)val == (Object)null))
			{
				Vector3 position = default(Vector3);
				((Vector3)(ref position))._002Ector(float.Parse(questZone.Position.X), float.Parse(questZone.Position.Y), float.Parse(questZone.Position.Z));
				Vector3 localScale = default(Vector3);
				((Vector3)(ref localScale))._002Ector(float.Parse(questZone.Scale.X), float.Parse(questZone.Scale.Y), float.Parse(questZone.Scale.Z));
				Quaternion rotation = default(Quaternion);
				((Quaternion)(ref rotation))._002Ector(float.Parse(questZone.Rotation.X), float.Parse(questZone.Rotation.Y), float.Parse(questZone.Rotation.Z), float.Parse(questZone.Rotation.W));
				val.transform.position = position;
				val.transform.rotation = rotation;
				val.transform.localScale = localScale;
				CustomZoneContainer item = new CustomZoneContainer(val, questZone.ZoneType, questZone.FlareType);
				Zones.Add(item);
			}
		});
		ZoneConfigManager.ExistingQuestZones.Clear();
	}

	public static void CreateNewZone()
	{
		string name = ZoneConfigManager.NewZoneName.Value;
		if (!string.IsNullOrWhiteSpace(name) && !Zones.Exists((CustomZoneContainer z) => ((Object)z.GameObject).name == name))
		{
			string value = ZoneConfigManager.NewZoneType.Value;
			string flareZoneType = (string.IsNullOrEmpty(ZoneConfigManager.FlareZoneType.Value) ? "" : ZoneConfigManager.FlareZoneType.Value);
			GameObject val = Utils.CreateNewZoneCube(name);
			if (!((Object)(object)val == (Object)null))
			{
				Zones.Add(new CustomZoneContainer(val, value, flareZoneType));
				LogHelper.LogDebug($"Created new zone: {name}, total zones: {Zones.Count}");
			}
		}
	}

	public static void NextZone()
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1)
		{
			if (_currentSelectIndex >= 0)
			{
				Zones[_currentSelectIndex].GameObject.GetComponent<Renderer>().material.color = ZoneConfigManager.ColorZoneRed;
			}
			if (_currentSelectIndex + 1 < Zones.Count)
			{
				_currentSelectIndex++;
			}
			else
			{
				_currentSelectIndex = 0;
			}
			Zones[_currentSelectIndex].GameObject.GetComponent<Renderer>().material.color = ZoneConfigManager.ColorZoneGreen;
			_selectedZoneName = ((Object)Zones[_currentSelectIndex].GameObject).name;
			AdjustConfigValues();
		}
	}

	public static void PrevZone()
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1)
		{
			if (_currentSelectIndex >= 0)
			{
				Zones[_currentSelectIndex].GameObject.GetComponent<Renderer>().material.color = ZoneConfigManager.ColorZoneRed;
			}
			if (_currentSelectIndex - 1 < 0)
			{
				_currentSelectIndex = Zones.Count - 1;
			}
			else
			{
				_currentSelectIndex--;
			}
			Zones[_currentSelectIndex].GameObject.GetComponent<Renderer>().material.color = ZoneConfigManager.ColorZoneGreen;
			_selectedZoneName = ((Object)Zones[_currentSelectIndex].GameObject).name;
			AdjustConfigValues();
		}
	}

	public static string GetCurrentZoneName()
	{
		return _selectedZoneName;
	}

	public static void AdjustPosition()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1 && _currentSelectIndex >= 0)
		{
			GameObject gameObject = Zones[_currentSelectIndex].GameObject;
			Vector3 position = default(Vector3);
			((Vector3)(ref position))._002Ector(ZoneConfigManager.PositionConfigX.Value, ZoneConfigManager.PositionConfigY.Value, ZoneConfigManager.PositionConfigZ.Value);
			gameObject.transform.position = position;
		}
	}

	public static void AdjustScale()
	{
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1 && _currentSelectIndex >= 0)
		{
			GameObject gameObject = Zones[_currentSelectIndex].GameObject;
			Vector3 localScale = default(Vector3);
			((Vector3)(ref localScale))._002Ector(ZoneConfigManager.ScaleConfigX.Value, ZoneConfigManager.ScaleConfigY.Value, ZoneConfigManager.ScaleConfigZ.Value);
			gameObject.transform.localScale = localScale;
		}
	}

	public static void AdjustRotation()
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1 && _currentSelectIndex >= 0)
		{
			GameObject gameObject = Zones[_currentSelectIndex].GameObject;
			Quaternion rotation = Quaternion.Euler(ZoneConfigManager.RotationConfigX.Value, ZoneConfigManager.RotationConfigY.Value, ZoneConfigManager.RotationConfigZ.Value);
			gameObject.transform.rotation = rotation;
		}
	}

	public static void OutputZones()
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1)
		{
			string text = Path.Combine(Path.GetFullPath(Path.Combine(Assembly.GetExecutingAssembly().Location, "..\\..\\..\\..\\")), $"WTT-ClientCommonLib-CustomQuestZone-Output-{DateTime.Now:yyyyMMddHHmmssffff}.json");
			using (StreamWriter streamWriter = File.CreateText(text))
			{
				List<CustomQuestZone> list = Utils.ConvertZoneFormat(Zones, Utils.GetLocationId());
				new JsonSerializer
				{
					Formatting = (Formatting)1
				}.Serialize((TextWriter)streamWriter, (object)list);
			}
			LogHelper.LogDebug("WTT-ClientCommonLib: Output zones to file: " + text);
		}
	}

	private static void AdjustConfigValues()
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		if (Zones.Count >= 1 && _currentSelectIndex >= 0)
		{
			GameObject gameObject = Zones[_currentSelectIndex].GameObject;
			ZoneConfigManager.PositionConfigX.Value = gameObject.transform.position.x;
			ZoneConfigManager.PositionConfigY.Value = gameObject.transform.position.y;
			ZoneConfigManager.PositionConfigZ.Value = gameObject.transform.position.z;
			ZoneConfigManager.ScaleConfigX.Value = gameObject.transform.localScale.x;
			ZoneConfigManager.ScaleConfigY.Value = gameObject.transform.localScale.y;
			ZoneConfigManager.ScaleConfigZ.Value = gameObject.transform.localScale.z;
			Quaternion rotation = gameObject.transform.rotation;
			Vector3 eulerAngles = ((Quaternion)(ref rotation)).eulerAngles;
			ZoneConfigManager.RotationConfigX.Value = eulerAngles.x;
			ZoneConfigManager.RotationConfigY.Value = eulerAngles.y;
			ZoneConfigManager.RotationConfigZ.Value = eulerAngles.z;
		}
	}
}
