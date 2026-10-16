using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using UnityEngine;

namespace WTTClientCommonLib.Models;

public class CustomSpawnConfig
{
	[CompilerGenerated]
	private Vector3 _003CPosition_003Ek__BackingField;

	[CompilerGenerated]
	private Vector3 _003CRotation_003Ek__BackingField;

	[JsonProperty("questId")]
	public string QuestId { get; set; }

	[JsonProperty("locationID")]
	public string LocationID { get; set; }

	[JsonProperty("bundleName")]
	public string BundleName { get; set; }

	[JsonProperty("prefabName")]
	public string PrefabName { get; set; }

	[JsonProperty("position")]
	public Vector3 Position
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CPosition_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CPosition_003Ek__BackingField = value;
		}
	}

	[JsonProperty("rotation")]
	public Vector3 Rotation
	{
		[CompilerGenerated]
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return _003CRotation_003Ek__BackingField;
		}
		[CompilerGenerated]
		set
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0002: Unknown result type (might be due to invalid IL or missing references)
			_003CRotation_003Ek__BackingField = value;
		}
	}

	[JsonProperty("requiredQuestStatuses")]
	public List<string> RequiredQuestStatuses { get; set; }

	[JsonProperty("excludedQuestStatuses")]
	public List<string> ExcludedQuestStatuses { get; set; }

	[JsonProperty("questMustExist")]
	public bool? QuestMustExist { get; set; }

	[JsonProperty("linkedQuestId")]
	public string LinkedQuestId { get; set; }

	[JsonProperty("linkedRequiredStatuses")]
	public List<string> LinkedRequiredStatuses { get; set; }

	[JsonProperty("linkedExcludedStatuses")]
	public List<string> LinkedExcludedStatuses { get; set; }

	[JsonProperty("linkedQuestMustExist")]
	public bool? LinkedQuestMustExist { get; set; }

	[JsonProperty("requiredItemInInventory")]
	public string RequiredItemInInventory { get; set; }

	[JsonProperty("requiredLevel")]
	public int? RequiredLevel { get; set; }

	[JsonProperty("requiredFaction")]
	public string RequiredFaction { get; set; }

	[JsonProperty("requiredBossSpawned")]
	public string RequiredBossSpawned { get; set; }

	public CustomSpawnConfig()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		Position = Vector3.zero;
		Rotation = Vector3.zero;
		RequiredQuestStatuses = new List<string>();
		ExcludedQuestStatuses = new List<string>();
		LinkedRequiredStatuses = new List<string>();
		LinkedExcludedStatuses = new List<string>();
		base._002Ector();
	}
}
