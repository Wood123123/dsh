using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace RAID_REVIEW;

internal class Phobos_Integration
{
	private const string AssemblyName = "Phobos";

	private static bool _reflectionInit = false;

	private static bool _available = false;

	private static PropertyInfo _singletonInstanceProp;

	private static FieldInfo _agentDataField;

	private static FieldInfo _entitiesField;

	private static FieldInfo _valuesField;

	private static FieldInfo _agentIsActiveField;

	private static FieldInfo _agentIsLeaderField;

	private static FieldInfo _agentPlayerField;

	private static FieldInfo _agentObjectiveField;

	private static FieldInfo _objStatusField;

	private static FieldInfo _objLocationField;

	private static FieldInfo _locPositionField;

	private static FieldInfo _locCategoryField;

	private static FieldInfo _locationSystemField;

	private static PropertyInfo _gridSizeProp;

	private static PropertyInfo _worldMinProp;

	private static PropertyInfo _cellSizeProp;

	private static PropertyInfo _advectionFieldProp;

	private static PropertyInfo _convergenceFieldProp;

	private static PropertyInfo _zonesProp;

	private static FieldInfo _cellsField;

	private static FieldInfo _cellLocationsField;

	private static FieldInfo _locationIdField;

	private static FieldInfo _locationNameField;

	private static FieldInfo _zoneCoordsField;

	private static FieldInfo _zoneRadiusField;

	private static FieldInfo _zoneForceField;

	private static FieldInfo _zoneDecayField;

	private static Dictionary<string, object> _agentCache = new Dictionary<string, object>();

	private static Type ResolvePhobosType(string typeFullName)
	{
		return Type.GetType(typeFullName + ", Phobos");
	}

	public static void InitReflection()
	{
		if (_reflectionInit)
		{
			return;
		}
		_reflectionInit = true;
		try
		{
			Type type = ResolvePhobosType("Phobos.Orchestration.PhobosManager");
			if (type == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: PHOBOS :::: PhobosManager type not found in any known Phobos assembly");
				return;
			}
			LoggerInstance.Log.LogInfo((object)"RAID_REVIEW :::: PHOBOS :::: Resolved Phobos types from assembly 'Phobos'");
			_singletonInstanceProp = typeof(Singleton<>).MakeGenericType(type).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
			_agentDataField = type.GetField("AgentData", BindingFlags.Instance | BindingFlags.Public);
			if (_agentDataField == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: PHOBOS :::: AgentData field not found");
				return;
			}
			_entitiesField = _agentDataField.FieldType.GetField("Entities", BindingFlags.Instance | BindingFlags.Public);
			if (_entitiesField == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: PHOBOS :::: Entities field not found");
				return;
			}
			_valuesField = _entitiesField.FieldType.GetField("Values", BindingFlags.Instance | BindingFlags.Public);
			if (_valuesField == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: PHOBOS :::: Values field not found");
				return;
			}
			Type type2 = ResolvePhobosType("Phobos.Entities.Agent");
			if (type2 == null)
			{
				LoggerInstance.Log.LogWarning((object)"RAID_REVIEW :::: PHOBOS :::: Agent type not found");
				return;
			}
			_agentIsActiveField = type2.GetField("IsActive", BindingFlags.Instance | BindingFlags.Public);
			_agentIsLeaderField = type2.GetField("IsLeader", BindingFlags.Instance | BindingFlags.Public);
			_agentPlayerField = type2.GetField("Player", BindingFlags.Instance | BindingFlags.Public);
			_agentObjectiveField = type2.GetField("Objective", BindingFlags.Instance | BindingFlags.Public);
			if (_agentObjectiveField != null)
			{
				Type fieldType = _agentObjectiveField.FieldType;
				_objStatusField = fieldType.GetField("Status", BindingFlags.Instance | BindingFlags.Public);
				_objLocationField = fieldType.GetField("Location", BindingFlags.Instance | BindingFlags.Public);
				if (_objLocationField != null)
				{
					Type fieldType2 = _objLocationField.FieldType;
					_locPositionField = fieldType2.GetField("Position", BindingFlags.Instance | BindingFlags.Public);
					_locCategoryField = fieldType2.GetField("Category", BindingFlags.Instance | BindingFlags.Public);
				}
			}
			_locationSystemField = type.GetField("LocationSystem", BindingFlags.Instance | BindingFlags.Public);
			if (_locationSystemField != null)
			{
				Type fieldType3 = _locationSystemField.FieldType;
				_gridSizeProp = fieldType3.GetProperty("GridSize", BindingFlags.Instance | BindingFlags.Public);
				_worldMinProp = fieldType3.GetProperty("WorldMin", BindingFlags.Instance | BindingFlags.Public);
				_cellSizeProp = fieldType3.GetProperty("CellSize", BindingFlags.Instance | BindingFlags.Public);
				_advectionFieldProp = fieldType3.GetProperty("AdvectionField", BindingFlags.Instance | BindingFlags.Public);
				_convergenceFieldProp = fieldType3.GetProperty("ConvergenceField", BindingFlags.Instance | BindingFlags.Public);
				_zonesProp = fieldType3.GetProperty("Zones", BindingFlags.Instance | BindingFlags.Public);
				Type type3 = ResolvePhobosType("Phobos.Systems.LocationSystem+Zone");
				if (type3 != null)
				{
					_zoneCoordsField = type3.GetField("Coords", BindingFlags.Instance | BindingFlags.Public);
					_zoneRadiusField = type3.GetField("Radius", BindingFlags.Instance | BindingFlags.Public);
					_zoneForceField = type3.GetField("Force", BindingFlags.Instance | BindingFlags.Public);
					_zoneDecayField = type3.GetField("Decay", BindingFlags.Instance | BindingFlags.Public);
				}
				_cellsField = fieldType3.GetField("_cells", BindingFlags.Instance | BindingFlags.NonPublic);
				Type type4 = ResolvePhobosType("Phobos.Systems.LocationSystem+Cell") ?? ResolvePhobosType("Phobos.Systems.Cell");
				if (type4 != null)
				{
					_cellLocationsField = type4.GetField("Locations", BindingFlags.Instance | BindingFlags.Public);
				}
				Type type5 = ResolvePhobosType("Phobos.Navigation.Location");
				if (type5 != null)
				{
					_locationIdField = type5.GetField("Id", BindingFlags.Instance | BindingFlags.Public);
					_locationNameField = type5.GetField("Name", BindingFlags.Instance | BindingFlags.Public);
				}
			}
			_available = _singletonInstanceProp != null && _agentPlayerField != null && _agentObjectiveField != null && _objStatusField != null;
			LoggerInstance.Log.LogInfo((object)("RAID_REVIEW :::: PHOBOS :::: Reflection init OK — " + $"Instance={_singletonInstanceProp != null}, AgentData={_agentDataField != null}, " + $"Entities={_entitiesField != null}, Values={_valuesField != null}, " + $"IsActive={_agentIsActiveField != null}, IsLeader={_agentIsLeaderField != null}, " + $"Player={_agentPlayerField != null}, Objective={_agentObjectiveField != null}, " + $"Status={_objStatusField != null}, Location={_objLocationField != null}, " + $"LocPos={_locPositionField != null}, LocCat={_locCategoryField != null}, " + $"LocationSystem={_locationSystemField != null}, AdvectionField={_advectionFieldProp != null}, " + $"Zones={_zonesProp != null}, ZoneCoords={_zoneCoordsField != null}"));
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogWarning((object)("RAID_REVIEW :::: PHOBOS :::: Reflection init failed: " + ex.Message));
		}
	}

	public static void RefreshAgentCache()
	{
		_agentCache.Clear();
		if (!_available)
		{
			return;
		}
		try
		{
			object value = _singletonInstanceProp.GetValue(null);
			if (value == null)
			{
				return;
			}
			object value2 = _agentDataField.GetValue(value);
			if (value2 == null)
			{
				return;
			}
			object value3 = _entitiesField.GetValue(value2);
			if (value3 == null || !(_valuesField.GetValue(value3) is IEnumerable enumerable))
			{
				return;
			}
			foreach (object item in enumerable)
			{
				if (item == null)
				{
					continue;
				}
				try
				{
					object value4 = _agentPlayerField.GetValue(item);
					Player val = (Player)((value4 is Player) ? value4 : null);
					if ((Object)(object)val != (Object)null && !string.IsNullOrEmpty(val.ProfileId))
					{
						_agentCache[val.ProfileId] = item;
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogError((object)("RAID_REVIEW :::: PHOBOS :::: RefreshAgentCache failed: " + ex.Message));
		}
	}

	public static TrackingBotObjective GetBotObjectiveData(Player player, string sessionId, long time)
	{
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		//IL_0159: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0164: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		if (!_available || (Object)(object)player == (Object)null || !player.IsAI)
		{
			return null;
		}
		try
		{
			if (!_agentCache.TryGetValue(player.ProfileId, out var value) || value == null)
			{
				return null;
			}
			if (_agentIsActiveField != null)
			{
				object value2 = _agentIsActiveField.GetValue(value);
				if (value2 is bool && !(bool)value2)
				{
					return null;
				}
			}
			bool isLeader = false;
			if (_agentIsLeaderField != null)
			{
				object value3 = _agentIsLeaderField.GetValue(value);
				if (value3 is bool && (bool)value3)
				{
					isLeader = true;
				}
			}
			object value4 = _agentObjectiveField.GetValue(value);
			if (value4 == null)
			{
				return null;
			}
			string status = _objStatusField.GetValue(value4)?.ToString() ?? "None";
			string category = "";
			float objectiveX = 0f;
			float objectiveY = 0f;
			float objectiveZ = 0f;
			object obj = _objLocationField?.GetValue(value4);
			if (obj != null)
			{
				category = _locCategoryField?.GetValue(obj)?.ToString() ?? "";
				if (_locPositionField?.GetValue(obj) is Vector3 val)
				{
					objectiveX = val.x;
					objectiveY = val.y;
					objectiveZ = val.z;
				}
			}
			return new TrackingBotObjective
			{
				sessionId = sessionId,
				profileId = player.ProfileId,
				time = time,
				status = status,
				category = category,
				isLeader = isLeader,
				objectiveX = objectiveX,
				objectiveY = objectiveY,
				objectiveZ = objectiveZ
			};
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogError((object)("RAID_REVIEW :::: PHOBOS :::: Error extracting objective for " + player.ProfileId + ": " + ex.Message));
			return null;
		}
	}

	private static List<TrackingPhobosCell> ExtractField(Array field, int cols, int rows)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		List<TrackingPhobosCell> list = new List<TrackingPhobosCell>();
		if (field == null)
		{
			return list;
		}
		for (int i = 0; i < cols; i++)
		{
			for (int j = 0; j < rows; j++)
			{
				Vector2 val = (Vector2)field.GetValue(i, j);
				if (!(val.x * val.x + val.y * val.y < 0.0001f))
				{
					list.Add(new TrackingPhobosCell
					{
						x = i,
						y = j,
						fx = val.x,
						fz = val.y
					});
				}
			}
		}
		return list;
	}

	public static TrackingPhobosField GetAdvectionFieldSnapshot(string sessionId, long time)
	{
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_016e: Unknown result type (might be due to invalid IL or missing references)
		if (!_available || _locationSystemField == null || _advectionFieldProp == null)
		{
			return null;
		}
		try
		{
			object value = _singletonInstanceProp.GetValue(null);
			if (value == null)
			{
				return null;
			}
			object value2 = _locationSystemField.GetValue(value);
			if (value2 == null)
			{
				return null;
			}
			object obj = _gridSizeProp?.GetValue(value2);
			object obj2 = _worldMinProp?.GetValue(value2);
			object obj3 = _cellSizeProp?.GetValue(value2);
			if (obj == null || obj2 == null || obj3 == null)
			{
				return null;
			}
			Vector2Int val = (Vector2Int)obj;
			Vector2 val2 = (Vector2)obj2;
			float cellSize = (float)obj3;
			List<TrackingPhobosCell> list = ExtractField(_advectionFieldProp.GetValue(value2) as Array, ((Vector2Int)(ref val)).x, ((Vector2Int)(ref val)).y);
			List<TrackingPhobosCell> list2 = ExtractField(_convergenceFieldProp?.GetValue(value2) as Array, ((Vector2Int)(ref val)).x, ((Vector2Int)(ref val)).y);
			List<TrackingPhobosZone> list3 = new List<TrackingPhobosZone>();
			if (_zonesProp?.GetValue(value2) is IEnumerable enumerable && _zoneCoordsField != null)
			{
				foreach (object item in enumerable)
				{
					if (item != null)
					{
						Vector2Int val3 = (Vector2Int)_zoneCoordsField.GetValue(item);
						float radius = ((_zoneRadiusField != null) ? ((float)_zoneRadiusField.GetValue(item)) : 0f);
						float force = ((_zoneForceField != null) ? ((float)_zoneForceField.GetValue(item)) : 0f);
						float decay = ((_zoneDecayField != null) ? ((float)_zoneDecayField.GetValue(item)) : 0f);
						list3.Add(new TrackingPhobosZone
						{
							x = ((Vector2Int)(ref val3)).x,
							y = ((Vector2Int)(ref val3)).y,
							radius = radius,
							force = force,
							decay = decay
						});
					}
				}
			}
			if (list.Count == 0 && list2.Count == 0 && list3.Count == 0)
			{
				return null;
			}
			return new TrackingPhobosField
			{
				sessionId = sessionId,
				time = time,
				gridCols = ((Vector2Int)(ref val)).x,
				gridRows = ((Vector2Int)(ref val)).y,
				worldMinX = val2.x,
				worldMinZ = val2.y,
				cellSize = cellSize,
				advection = list,
				convergence = list2,
				zones = list3
			};
		}
		catch (Exception ex)
		{
			LoggerInstance.Log.LogError((object)("RAID_REVIEW :::: PHOBOS :::: GetAdvectionFieldSnapshot failed: " + ex.Message));
			return null;
		}
	}
}
