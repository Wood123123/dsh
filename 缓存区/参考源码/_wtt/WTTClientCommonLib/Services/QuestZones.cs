using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;
using WTTClientCommonLib.Components;
using WTTClientCommonLib.Helpers;
using WTTClientCommonLib.Models;

namespace WTTClientCommonLib.Services;

internal class QuestZones
{
	private static readonly HashSet<string> _usedGroupPositions = new HashSet<string>();

	private static readonly List<SalvageItemTrigger.SalvageReward> _salvageRewards = new List<SalvageItemTrigger.SalvageReward>();

	private static string PosKey(ZoneTransform p)
	{
		return p.X + "|" + p.Y + "|" + p.Z;
	}

	public static List<CustomQuestZone> GetZones()
	{
		List<CustomQuestZone> list = Utils.Get<List<CustomQuestZone>>("/wttcommonlib/zones/get");
		if (list == null || list.Count == 0)
		{
			LogHelper.LogDebug("[QuestZones.GetZones] No zones data loaded.");
			return new List<CustomQuestZone>();
		}
		foreach (CustomQuestZone item in list)
		{
			if (item.Position != null)
			{
				ZoneTransform position = item.Position;
				if (position.W == null)
				{
					string text = (position.W = "0");
				}
				position = item.Rotation;
				if (position.W == null)
				{
					string text = (position.W = "0");
				}
				position = item.Scale;
				if (position.W == null)
				{
					string text = (position.W = "0");
				}
			}
			else
			{
				if (item.GroupPosition == null)
				{
					continue;
				}
				foreach (ZoneTransforms item2 in item.GroupPosition)
				{
					ZoneTransform position = item2.Position;
					if (position.W == null)
					{
						string text = (position.W = "0");
					}
					position = item2.Rotation;
					if (position.W == null)
					{
						string text = (position.W = "0");
					}
					position = item2.Scale;
					if (position.W == null)
					{
						string text = (position.W = "0");
					}
				}
			}
		}
		return list;
	}

	private static void ZoneCreateItem(CustomQuestZone customQuestZone)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject();
		((Collider)val.AddComponent<BoxCollider>()).isTrigger = true;
		Vector3 position = default(Vector3);
		((Vector3)(ref position))._002Ector(float.Parse(customQuestZone.Position.X), float.Parse(customQuestZone.Position.Y), float.Parse(customQuestZone.Position.Z));
		Vector3 localScale = default(Vector3);
		((Vector3)(ref localScale))._002Ector(float.Parse(customQuestZone.Scale.X), float.Parse(customQuestZone.Scale.Y), float.Parse(customQuestZone.Scale.Z));
		Quaternion rotation = default(Quaternion);
		((Quaternion)(ref rotation))._002Ector(float.Parse(customQuestZone.Rotation.X), float.Parse(customQuestZone.Rotation.Y), float.Parse(customQuestZone.Rotation.Z), float.Parse(customQuestZone.Rotation.W));
		val.transform.position = position;
		val.transform.localScale = localScale;
		val.transform.rotation = rotation;
		((TriggerWithId)val.AddComponent<PlaceItemTrigger>()).SetId(customQuestZone.ZoneId);
		val.layer = LayerMask.NameToLayer("Triggers");
		((Object)val).name = customQuestZone.ZoneId;
	}

	private static void ZoneCreateVisit(CustomQuestZone customQuestZone)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject();
		((Collider)val.AddComponent<BoxCollider>()).isTrigger = true;
		Vector3 position = default(Vector3);
		((Vector3)(ref position))._002Ector(float.Parse(customQuestZone.Position.X), float.Parse(customQuestZone.Position.Y), float.Parse(customQuestZone.Position.Z));
		Vector3 localScale = default(Vector3);
		((Vector3)(ref localScale))._002Ector(float.Parse(customQuestZone.Scale.X), float.Parse(customQuestZone.Scale.Y), float.Parse(customQuestZone.Scale.Z));
		Quaternion rotation = default(Quaternion);
		((Quaternion)(ref rotation))._002Ector(float.Parse(customQuestZone.Rotation.X), float.Parse(customQuestZone.Rotation.Y), float.Parse(customQuestZone.Rotation.Z), float.Parse(customQuestZone.Rotation.W));
		val.transform.position = position;
		val.transform.localScale = localScale;
		val.transform.rotation = rotation;
		((TriggerWithId)val.AddComponent<ExperienceTrigger>()).SetId(customQuestZone.ZoneId);
		val.layer = LayerMask.NameToLayer("Triggers");
		((Object)val).name = customQuestZone.ZoneId;
	}

	private static void ZoneCreateBotKillZone(CustomQuestZone customQuestZone)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject();
		((Collider)val.AddComponent<BoxCollider>()).isTrigger = true;
		Vector3 position = default(Vector3);
		((Vector3)(ref position))._002Ector(float.Parse(customQuestZone.Position.X), float.Parse(customQuestZone.Position.Y), float.Parse(customQuestZone.Position.Z));
		Vector3 localScale = default(Vector3);
		((Vector3)(ref localScale))._002Ector(float.Parse(customQuestZone.Scale.X), float.Parse(customQuestZone.Scale.Y), float.Parse(customQuestZone.Scale.Z));
		Quaternion rotation = default(Quaternion);
		((Quaternion)(ref rotation))._002Ector(float.Parse(customQuestZone.Rotation.X), float.Parse(customQuestZone.Rotation.Y), float.Parse(customQuestZone.Rotation.Z), float.Parse(customQuestZone.Rotation.W));
		val.transform.position = position;
		val.transform.localScale = localScale;
		val.transform.rotation = rotation;
		val.AddComponent<TriggerWithId>().SetId(customQuestZone.ZoneId);
		val.layer = LayerMask.NameToLayer("Triggers");
		((Object)val).name = customQuestZone.ZoneId;
	}

	private static void ZoneCreateSalvage(CustomQuestZone customQuestZone)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_0109: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		SalvageConfig salvage = customQuestZone.Salvage;
		if (salvage == null)
		{
			LogHelper.LogWarn("Salvage zone '" + customQuestZone.ZoneId + "' missing Salvage config");
			return;
		}
		GameObject val = new GameObject();
		((Collider)val.AddComponent<BoxCollider>()).isTrigger = true;
		Vector3 position = default(Vector3);
		((Vector3)(ref position))._002Ector(float.Parse(customQuestZone.Position.X), float.Parse(customQuestZone.Position.Y), float.Parse(customQuestZone.Position.Z));
		Vector3 localScale = default(Vector3);
		((Vector3)(ref localScale))._002Ector(float.Parse(customQuestZone.Scale.X), float.Parse(customQuestZone.Scale.Y), float.Parse(customQuestZone.Scale.Z));
		Quaternion rotation = default(Quaternion);
		((Quaternion)(ref rotation))._002Ector(float.Parse(customQuestZone.Rotation.X), float.Parse(customQuestZone.Rotation.Y), float.Parse(customQuestZone.Rotation.Z), float.Parse(customQuestZone.Rotation.W));
		val.transform.position = position;
		val.transform.localScale = localScale;
		val.transform.rotation = rotation;
		SalvageItemTrigger salvageItemTrigger = val.AddComponent<SalvageItemTrigger>();
		((TriggerWithId)salvageItemTrigger).SetId(customQuestZone.ZoneId);
		IEnumerable<SalvageItemTrigger.SalvageReward> enumerable = salvage.Rewards?.Select((SalvageRewardConfig r) => new SalvageItemTrigger.SalvageReward
		{
			ItemTpl = r.ItemTpl,
			Count = r.Count,
			ToQuestInventory = r.ToQuestInventory
		}) ?? Array.Empty<SalvageItemTrigger.SalvageReward>();
		_salvageRewards.AddRange(enumerable);
		salvageItemTrigger.Configure(salvage.RequiredItemTpl, salvage.SalvageTime, enumerable, salvage.ConsumeRequiredItem);
		val.layer = LayerMask.NameToLayer("Triggers");
		((Object)val).name = customQuestZone.ZoneId;
	}

	private static void ZoneCreateFlareZone(CustomQuestZone customQuestZone)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_017d: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = new GameObject();
		BoxCollider val2 = val.AddComponent<BoxCollider>();
		((Collider)val2).isTrigger = true;
		Vector3 position = default(Vector3);
		((Vector3)(ref position))._002Ector(float.Parse(customQuestZone.Position.X), float.Parse(customQuestZone.Position.Y), float.Parse(customQuestZone.Position.Z));
		Vector3 localScale = default(Vector3);
		((Vector3)(ref localScale))._002Ector(float.Parse(customQuestZone.Scale.X), float.Parse(customQuestZone.Scale.Y), float.Parse(customQuestZone.Scale.Z));
		Quaternion rotation = default(Quaternion);
		((Quaternion)(ref rotation))._002Ector(float.Parse(customQuestZone.Rotation.X), float.Parse(customQuestZone.Rotation.Y), float.Parse(customQuestZone.Rotation.Z), float.Parse(customQuestZone.Rotation.W));
		val.transform.position = position;
		val.transform.localScale = localScale;
		val.transform.rotation = rotation;
		((TriggerWithId)val.AddComponent<ZoneFlareTrigger>()).SetId(customQuestZone.ZoneId);
		val.AddComponent<MoveObjectsToAdditionalPhysSceneMarker>();
		FlareShootDetectorZone obj = val.AddComponent<FlareShootDetectorZone>();
		Type typeFromHandle = typeof(FlareShootDetectorZone);
		BindingFlags bindingAttr = BindingFlags.Instance | BindingFlags.NonPublic;
		FieldInfo field = typeFromHandle.GetField("zoneID", bindingAttr);
		if (field != null)
		{
			field.SetValue(obj, customQuestZone.ZoneId);
		}
		FlareEventType val3 = (FlareEventType)Enum.Parse(typeof(FlareEventType), customQuestZone.FlareType);
		FieldInfo field2 = typeFromHandle.GetField("flareTypeForHandle", bindingAttr);
		if (field2 != null)
		{
			field2.SetValue(obj, val3);
		}
		PhysicsTriggerHandler val4 = val.AddComponent<PhysicsTriggerHandler>();
		val4.trigger = (Collider)(object)val2;
		FieldInfo field3 = typeFromHandle.GetField("_triggerHandlers", bindingAttr);
		if (field3 != null)
		{
			((List<PhysicsTriggerHandler>)field3.GetValue(obj)).Add(val4);
		}
		val.layer = LayerMask.NameToLayer("Triggers");
		((Object)val).name = customQuestZone.ZoneId;
	}

	public static void CreateZones(List<CustomQuestZone> zones)
	{
		_usedGroupPositions.Clear();
		_salvageRewards.Clear();
		foreach (CustomQuestZone zone in zones)
		{
			ApplyGroupPositionIfAny(zone);
			string text = zone.ZoneType.ToLowerInvariant();
			if (text == "placeitem")
			{
				ZoneCreateItem(zone);
			}
			if (text == "visit")
			{
				ZoneCreateVisit(zone);
			}
			if (text == "flarezone")
			{
				ZoneCreateFlareZone(zone);
			}
			if (text == "botkillzone")
			{
				ZoneCreateBotKillZone(zone);
			}
			if (text == "salvage")
			{
				ZoneCreateSalvage(zone);
			}
		}
		PreloadSalvageRewards();
	}

	private static async Task PreloadSalvageRewards()
	{
		ObjectsFactory instance = Singleton<ObjectsFactory>.Instance;
		if (instance == null)
		{
			return;
		}
		ItemFactory instance2 = Singleton<ItemFactory>.Instance;
		if (instance2 == null)
		{
			return;
		}
		List<ResourceKey> list = new List<ResourceKey>();
		foreach (SalvageItemTrigger.SalvageReward salvageReward in _salvageRewards)
		{
			if (((Dictionary<MongoID, ItemTemplate>)(object)instance2.ItemTemplates).TryGetValue(MongoID.op_Implicit(salvageReward.ItemTpl), out ItemTemplate value))
			{
				if (value.Prefab != (ResourceKey)null)
				{
					list.Add(value.Prefab);
				}
				if (value.UsePrefab != (ResourceKey)null)
				{
					list.Add(value.UsePrefab);
				}
			}
		}
		if (list.Count != 0)
		{
			await instance.LoadBundlesAndCreatePools((PoolsCategory)0, (AssemblyType)0, (ICollection<ResourceKey>)list.ToArray(), JobYieldPriority.Immediate, (IProgress<InitLevelProgress>)null, CancellationToken.None);
		}
	}

	private static void ApplyGroupPositionIfAny(CustomQuestZone zone)
	{
		if (zone.GroupPosition == null || zone.GroupPosition.Count == 0)
		{
			return;
		}
		List<int> list = new List<int>();
		for (int i = 0; i < zone.GroupPosition.Count; i++)
		{
			string item = PosKey(zone.GroupPosition[i].Position);
			if (!_usedGroupPositions.Contains(item))
			{
				list.Add(i);
			}
		}
		int index;
		if (list.Count > 0)
		{
			index = Random.Range(0, list.Count);
			index = list[index];
		}
		else
		{
			LogHelper.LogWarn("All GroupPosition positions for " + zone.ZoneId + " are already used; allowing reuse.");
			index = Random.Range(0, zone.GroupPosition.Count);
		}
		ZoneTransforms zoneTransforms = zone.GroupPosition[index];
		zone.Position = zoneTransforms.Position;
		zone.Rotation = zoneTransforms.Rotation;
		zone.Scale = zoneTransforms.Scale;
		string item2 = PosKey(zoneTransforms.Position);
		_usedGroupPositions.Add(item2);
	}
}
